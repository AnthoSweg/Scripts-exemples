using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using DG.Tweening;

public class Player : MonoBehaviour {
    [Header("Links")]
    [SerializeField]
    private Transform carryingPointTransform;
    public Animator animator;
    [SerializeField]
    private SkinnedMeshRenderer[] meshesWithColorSwap;
    [SerializeField]
    private GameObject spotLight;
    [SerializeField]
    private TrailRenderer dashTrail;
    public TextMeshPro playerNumberText;
    [SerializeField]
    private AudioClip[] grabClips;
    public GameObject buttonA;
    [SerializeField]
    private Projector directionProj;

    [Header("DEBUG")]
    public Camera cam;
    public float speedMalus;
    public float WalkSpeed
    {
        get { return Mathf.Clamp((GameParams.Main.playersSpeed - speedMalus), GameParams.Main.playerMinimumSpeed, GameParams.Main.playersSpeed); }
        private set { GameParams.Main.playersSpeed = value; }
    }
    private float rotationSpeed;
    public float RotationSpeed
    {
        get {
            rotationSpeed = WalkSpeed / 10;
            return rotationSpeed;
        }
        set { rotationSpeed = value; }
    }
    [SerializeField]
    private int score;
    public int Score
    {
        get { return score; }
        set {
            if (value != 0) {
                this.InstantiateText3D(value - score);
                stats.totalSold += value - score;
            }
            score = value;
            if (value != 0) {
                SessionManager.Main.UpdateScore();
                if (GameManager.Main.currentSession.sessionState != CustomEnums.SessionState.Overtime && this.scoreBar != null)
                    this.scoreBar.UpdateScore(Score);
            }
        }
    }
    public PlayerStats stats;
    public CustomEnums.PlayerState playerState;
    public string playerName;
    public int playerNumber;
    public int controllerNumber;
    public Furniture carryingFurniture;
    public Dictionary<Furniture, StaticUtils.ViewCastInfo> reachableFurnitures = new Dictionary<Furniture, StaticUtils.ViewCastInfo>();
    [SerializeField]
    private Furniture nearestFurniture;
    public int overallScore;
    public List<int> scoreTracker = new List<int>();
    
    private Vector3 vel;
    public static Vector3 anchorPos = new Vector3(0, 1.5f, .7f);
    public static Quaternion anchorRot;
    [HideInInspector]
    public PlayerInput Input { get; private set; }
    private AudioSource aSource;
    [HideInInspector]
    public DynamicScoreBar scoreBar;
    private List<StaticUtils.ViewCastInfo> viewcastsInfos = new List<StaticUtils.ViewCastInfo>();
    private Furniture lastNearestFurniture;
    [HideInInspector]
    public Rigidbody rb;
    [HideInInspector]
    public Material playerMat;
    [HideInInspector]
    public Transform playerTransform;
    private float joystickStrength;
    private Vector3 camForward;
    private Vector3 direction;
    [HideInInspector]
    public HingeJoint joint;
    [HideInInspector]
    public HingeJoint carryingPointJoint;
    private Rigidbody carryingPointRb;
    private Vector3 lastPos;
    private float timeWithoutMoving = 0;
    private float walkDustDistance = 5;
    //Used to cooldown abilities
    private float nextDash;
    private float nextInteract;

    public void Initialize() {
        this.playerTransform = transform;
        this.cam = GameParams.Main.menuCam;
        this.Input = this.GetComponent<PlayerInput>();
        this.rb = GetComponent<Rigidbody>();
        this.aSource = GetComponent<AudioSource>();
        this.stats = GetComponent<PlayerStats>();
        this.directionProj.material = new Material(GameParams.Main.directionMat);
        this.carryingPointRb = this.carryingPointTransform.GetComponent<Rigidbody>();
        this.lastPos = playerTransform.position;
        this.ResetCarryingPoint();
        this.overallScore = 0;
        this.timeWithoutMoving = 0;

        if (GameParams.Main.TESTSCENE) {
            playerTransform.position = StaticUtils.RandomCircle(GameManager.Main.currentSession.SpawnPoint, 2);
            this.cam = GameParams.Main.sessionCam.cam;
            MultipleTargetCamera.targets.Add(this.playerTransform);
        }
    }

    private void Update() {
        if (this.playerState != CustomEnums.PlayerState.sleep
            && (GameManager.Main.gameState == CustomEnums.GameState.Menu
            || (GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.NormalGame)
            || (GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.Overtime)
            )) {
            if (playerState == CustomEnums.PlayerState.normal && Time.frameCount % 5 == 0)
                nearestFurniture = GetNearestFurniture(false);

            if (Input.InteractIsPressed && Time.time > nextInteract) {
                nextInteract = Time.time + .2f;
                OnInteractionPressed();
            }
            if (Input.DashIsPressed && Time.time > nextDash) {
                nextDash = Time.time + GameParams.Main.playerDashCooldown;
                Dash();
            }
        }

        //STATS
        if ((GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.NormalGame)
            || (GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.Overtime)
            ) {
            stats.distanceTravelled += Vector3.Distance(playerTransform.position, lastPos);

            if (this.carryingFurniture != null && (this.carryingFurniture.data.furnitureWeight == CustomEnums.WeightEnum.heavy || this.carryingFurniture.data.furnitureWeight == CustomEnums.WeightEnum.superHeavy))
                stats.distanceTravelledWithHeavyObjects += Vector3.Distance(playerTransform.position, lastPos);

            if (this.carryingFurniture != null && (this.carryingFurniture.playersCarrying.Count > 1))
                stats.distanceTravelledTogether += Vector3.Distance(playerTransform.position, lastPos);

            //ANTI BUG CHECK
            if (Vector3.Distance(playerTransform.position, lastPos) == 0) {
                timeWithoutMoving += Time.deltaTime;
                if (timeWithoutMoving > 3) {
                    if (carryingFurniture == null)
                        playerState = CustomEnums.PlayerState.normal;
                    else if (carryingFurniture.playersCarrying.Count == 1)
                        playerState = CustomEnums.PlayerState.carrying;
                    else
                        playerState = CustomEnums.PlayerState.carryingWithOthers;

                    timeWithoutMoving = 0;
                }
            }
        }

        if (walkDustDistance > 3) {
            DustParticules();
            walkDustDistance = 0;
        }

        walkDustDistance += Vector3.Distance(playerTransform.position, lastPos);
        lastPos = this.playerTransform.position;
    }

    private void FixedUpdate() {
        if (this.playerState != CustomEnums.PlayerState.sleep
            && Time.timeScale != 0
            && (GameManager.Main.gameState == CustomEnums.GameState.Menu
            || (GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.NormalGame)
            || (GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.Overtime)
            ))
            Move();
        else {
            rb.velocity = Vector3.zero;
            joystickStrength = 0;
        }
        animator.SetFloat("speed", joystickStrength);
    }

    private void OnInteractionPressed() {
        switch (playerState) {
            case CustomEnums.PlayerState.normal:
                PickUp(GetNearestFurniture(true));
                break;
            case CustomEnums.PlayerState.carrying:
                Drop();
                break;
            case CustomEnums.PlayerState.carryingWithOthers:
                Drop();
                break;
            default:
                break;
        }
    }

    private void Move() {
        joystickStrength = new Vector2(Input.Horizontal, Input.Vertical).magnitude;
        Mathf.Clamp(joystickStrength, 0, 1);

        if (cam != null) {
            camForward = Vector3.Scale(cam.transform.forward, new Vector3(1, 0, 1)).normalized;
            direction = Input.Vertical * camForward + Input.Horizontal * cam.transform.right;
        }

        direction.Normalize();

        if (playerState == CustomEnums.PlayerState.carryingWithOthers) {
            playerTransform.LookAt(new Vector3(this.carryingPointTransform.transform.position.x, this.playerTransform.position.y, this.carryingPointTransform.transform.position.z), Vector3.up);
            if (joystickStrength > .25f) {
                vel = rb.velocity;
                vel.x = direction.x * Time.fixedDeltaTime * (WalkSpeed) * 100 * joystickStrength;
                vel.y = 0;
                vel.z = direction.z * Time.fixedDeltaTime * (WalkSpeed) * 100 * joystickStrength;

                rb.velocity = vel;

            }
            else {
                if (this.carryingFurniture != null) {
                    carryingFurniture.pivotPoint = this.playerTransform.position;
                    rb.velocity = Vector3.zero;
                }
                else
                    playerState = CustomEnums.PlayerState.normal;
            }
        }
        else {
            if (joystickStrength > .25f) {
                playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, Quaternion.LookRotation(direction), RotationSpeed);

                vel = rb.velocity;
                vel.x = direction.x * Time.fixedDeltaTime * (WalkSpeed) * 100 * joystickStrength;
                vel.y = 0;
                vel.z = direction.z * Time.fixedDeltaTime * (WalkSpeed) * 100 * joystickStrength;

                rb.velocity = vel;
            }
            else
                rb.velocity = Vector3.zero;
        }
    }

    public async void PickUp(Furniture fur, bool instant = false) {
        if (fur != null) {
            if (this.carryingFurniture == null) {
                PlayRandomGrabSound();
                if (fur.playersCarrying.Count != 0)
                    instant = true;

                this.animator.SetBool("carrying", true);
                if (instant == false) {
                    this.playerState = CustomEnums.PlayerState.sleep;
                    await Task.Delay(TimeSpan.FromSeconds(GameParams.Main.carrySleepTimeInSeconds));
                }
                if (fur == null) {
                    playerState = CustomEnums.PlayerState.normal;
                        return;
                }
                fur.OnPickedUp(this, instant);
                lastNearestFurniture = null;
                nearestFurniture = null;
            }
        }
    }

    public void Drop() {
        if (this.carryingFurniture != null && this.playerState != CustomEnums.PlayerState.sleep) {
            this.animator.SetBool("carrying", false);
            this.carryingFurniture.OnDropped(this);
            this.carryingFurniture = null;
            this.playerState = CustomEnums.PlayerState.normal;
            this.ResetCarryingPoint();
        }
    }

    private async void Dash() {
        //SET UP
        CustomEnums.PlayerState previousState = this.playerState;
        this.playerState = CustomEnums.PlayerState.dashing;

        if (this.carryingFurniture != null && carryingFurniture.playersCarrying.Count > 1) {
            for (int i = this.carryingFurniture.playersCarrying.Count - 1; i >= 0; i--) {
                if (this.carryingFurniture.playersCarrying[i] != this)
                    this.carryingFurniture.playersCarrying[i].Drop();
            }
        }

        this.animator.SetBool("dash", true);
        if (dashTrail != null)
            this.dashTrail.emitting = true;

        Instantiate(GameParams.Main.dashParticuleEffect, this.playerTransform.position, this.playerTransform.rotation);

        float actualMalus = this.speedMalus;
        float t = 0;
        float slow = 1;
        if (this.carryingFurniture != null)
            slow = this.carryingFurniture.weight.Remap(GameParams.Main.weights[0], GameParams.Main.weights[GameParams.Main.weights.Length - 1], .5f, 2.5f);

        //ACTUALLY MOVING
        while (t <= (GameParams.Main.dashTime)) {
            this.rb.AddForce(transform.forward.normalized * ((GameParams.Main.dashDistance / (slow * GameParams.Main.dashSlowAccentuation)) / GameParams.Main.dashTime), ForceMode.Acceleration);
            t += Time.fixedDeltaTime;
            await Task.Delay(1);
        }

        //DASH END
        if (this.playerState == CustomEnums.PlayerState.dashing)
            this.playerState = previousState;

        this.animator.SetBool("dash", false);
        if (dashTrail != null) {
            this.dashTrail.emitting = false;
        }
        if (this.carryingFurniture == null) {
            this.speedMalus = 5;
            await Task.Delay(TimeSpan.FromSeconds(GameParams.Main.dashSlowDuration));
            this.speedMalus = 0;
        }
    }

    private Furniture GetNearestFurniture(bool tryingToGrab) {
        float stepAngleSize = GameParams.Main.viewAngle / 2;
        reachableFurnitures.Clear();
        for (float i = 0; i <= 2f; i += 2 / 10f) {
            for (int j = 0; j <= 2; j++) {
                float angle = transform.eulerAngles.y - GameParams.Main.viewAngle / 2 + stepAngleSize * j;
                StaticUtils.ViewCastInfo info = StaticUtils.ViewCast(angle, GameParams.Main.viewLength, (new Vector3(playerTransform.position.x, playerTransform.position.y + i + 0.05f, playerTransform.position.z)));
                if (info.hit) {
                    Furniture f = info.obj.GetComponent<Furniture>();
                    if (f != null && !reachableFurnitures.ContainsKey(f)) {
                        reachableFurnitures.Add(f, info);
                    }
                }
            }
        }

        //Default value is an unreachable one to be sure
        float distance = -10;
        Furniture fur = null;
        for (int i = 0; i < reachableFurnitures.Values.Count; i++) {
            if (reachableFurnitures.ElementAt(i).Value.dst < distance || distance == -10) {
                distance = reachableFurnitures.ElementAt(i).Value.dst;
                fur = reachableFurnitures.ElementAt(i).Key;

                if (tryingToGrab)
                    fur.detectionPoint = reachableFurnitures.ElementAt(i).Value.point;
            }
        }

        if (lastNearestFurniture != fur) {
            if (lastNearestFurniture != null)
                lastNearestFurniture.OnDefocused();
            if (fur != null)
                fur.OnFocused(this);
        }

        lastNearestFurniture = fur;
        if (GameManager.Main.gameState == CustomEnums.GameState.Menu) {
            if (fur != null)
                buttonA.SetActive(true);
            else
                buttonA.SetActive(false);
        }
        return fur;
    }

    public Vector3 SnapToPlayer(Furniture fur, Vector3 hitPoint) {
        Vector3 offset = (fur.fTransform.position - hitPoint);
        return new Vector3((carryingPointTransform.transform.position + offset).x, carryingPointTransform.transform.position.y, (carryingPointTransform.transform.position + offset).z);
    }

    //Used to carry furnitures
    public void CreateJoints(Rigidbody rb, bool freeze = false) {
        this.joint = this.gameObject.AddComponent<HingeJoint>();
        this.joint.anchor = new Vector3(0, 1, 0);
        this.joint.axis = new Vector3(0, 90, 0);
        this.joint.enablePreprocessing = false;

        this.carryingPointJoint = this.carryingPointTransform.gameObject.AddComponent<HingeJoint>();
        this.carryingPointJoint.anchor = Vector3.zero;
        this.carryingPointJoint.axis = new Vector3(0, 90, 0);
        this.carryingPointJoint.useLimits = true;
        this.carryingPointJoint.enablePreprocessing = false;
        this.carryingPointJoint.breakForce = GameParams.Main.breakForce;

        this.joint.connectedBody = this.carryingPointJoint.GetComponent<Rigidbody>();
        this.carryingPointJoint.connectedBody = rb;
        this.carryingPointRb.isKinematic = false;
        if (freeze)
            this.carryingPointRb.constraints = RigidbodyConstraints.FreezePosition;
        else
            this.carryingPointRb.constraints = RigidbodyConstraints.None;
    }

    public void UpdateJoints(bool solo, bool noSpring = false) {
        if (this.joint != null) {

            if (solo) {
                this.joint.useLimits = false;
                if (noSpring == false) {
                    this.joint.useSpring = true;
                    JointSpring js = this.joint.spring;
                    js.spring = 1000f;
                    this.joint.spring = js;
                }
            } else {
                this.joint.useLimits = true;
                this.joint.useSpring = false;
            }
        }
    }

    public void DeleteJoints() {

        if (this.joint != null)
            Destroy(this.joint);
        if (this.carryingPointJoint != null) {
            this.carryingPointRb.isKinematic = true;
            Destroy(this.carryingPointJoint);
        }
    }

    public void SwitchColor(colorSwapZone czs) {
        if (czs.isUsed)
            return;
        if (this.playerMat != null)
            GameManager.Main.matsAvailable.Find(x => x.material == this.playerMat).isUsed = false;

        czs.isUsed = true;
        playerMat = czs.material;
        for (int i = 0; i < this.meshesWithColorSwap.Length; i++) {
            this.meshesWithColorSwap[i].material = playerMat;
        }
        //SpriteIcone.GetComponent<SpriteRenderer>().material = playerMat;
        this.directionProj.material.color = this.playerMat.color;
        this.playerName = czs.nameForThePlayer;
        this.playerNumberText.color = playerMat.color;
        this.playerNumberText.text = "P" + playerNumber + " " + this.playerName;
        this.GetComponentInChildren<SeeTroughtOutline>().OutlineColor = playerMat.color;
    }

    public void ResetCarryingPoint() {
        this.carryingPointTransform.localPosition = anchorPos;
        this.carryingPointTransform.localRotation = anchorRot;
    }

    public void HighlightPlayer() {
        spotLight.SetActive(true);
    }

    public void UnHighlightPlayer() {
        spotLight.SetActive(false);
    }

    private void PlayRandomGrabSound() {
        int r = UnityEngine.Random.Range(0, grabClips.Length);
        aSource.clip = grabClips[r];
        aSource.Play();
    }

    private void DustParticules() {
        Instantiate(GameParams.Main.dustWalk, this.playerTransform.position, Quaternion.identity);
    }

    public void InstantiateText3D(int moneyGained) {
        GameObject moneyText = Instantiate(GameParams.Main.textMoney, this.playerTransform.position, Quaternion.Euler(90, GameParams.Main.sessionCam.transform.localEulerAngles.y, 0));
        TextMeshPro tmpro = moneyText.GetComponent<TextMeshPro>();
        tmpro.text = string.Format("+ {0}$", moneyGained);
        tmpro.color = this.playerMat.color;
        moneyText.transform.DOMoveY(moneyText.transform.position.y + 8, 4f, false);
        tmpro.DOFade(0, 4).SetEase(Ease.InQuart).OnComplete(() => {
            Destroy(moneyText);
        });
    }

    private void OnCollisionEnter(Collision collision) {
        if (this.playerState != CustomEnums.PlayerState.dashing)
            return;

        Furniture f = collision.gameObject.GetComponent<Furniture>();
        if (f != null && this.carryingFurniture == null && !f.playersCarrying.IsNullOrEmpty()) {
            if (!f.playersCarrying.Contains(this)) {

                for (int i = f.playersCarrying.Count - 1; i >= 0; i--) {
                    if ((GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.NormalGame)
                   || (GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.Overtime)
                   )
                        f.playersCarrying[i].stats.objectsIGotStolen++;
                    f.playersCarrying[i].Drop();
                }
                f.detectionPoint = collision.contacts[0].point;
                this.PickUp(f, true);
                if ((GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.NormalGame)
                || (GameManager.Main.gameState == CustomEnums.GameState.InSession && GameManager.Main.currentSession.sessionState == CustomEnums.SessionState.Overtime)
                )
                    this.stats.objectsIStole++;
            }
        }
    }
}