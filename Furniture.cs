using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
[SelectionBase]
public class Furniture : MonoBehaviour {
    [Header("Params")]
    public FurnitureData data;
    public GameObject[] specialEffects;
    
    [Header("DEBUG")]
    public List<Player> playersCarrying = new List<Player>();
    public int price;
    public float weight = 0;

    [HideInInspector] public Vector3 detectionPoint;
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public SeeTroughtOutline outline;
    [HideInInspector] public Transform fTransform;
    [HideInInspector] public Vector3 pivotPoint;
    [HideInInspector] public Player lastPlayer;
    [HideInInspector] public float baseMass;
    [HideInInspector] public BoxCollider col;

    public virtual void Initialize() {
        //Links
        this.rb = GetComponent<Rigidbody>();
        if (this.GetComponent<SeeTroughtOutline>() == null)
            this.outline = this.gameObject.AddComponent<SeeTroughtOutline>();
        else
            outline = this.GetComponent<SeeTroughtOutline>();
        this.col = GetComponent<BoxCollider>();
        this.fTransform = this.transform;
        this.col.material = GameParams.Main.furniturePhysicsMaterial;

        //Init data
        this.weight = GameParams.Main.weights[(int)data.furnitureWeight];
        this.baseMass = GameParams.Main.masses[(int)data.furnitureWeight];
        this.price = Mathf.RoundToInt(GameParams.Main.prices[(int)this.data.furnitureValue]
                    * UnityEngine.Random.Range(1f - (GameParams.Main.priceMultiplier / 100f), 1f + (GameParams.Main.priceMultiplier / 100f))
                    * ((int)data.furnitureWeight + 1) * .5f);
        this.rb.mass = baseMass;
        this.gameObject.AddComponent<NavMeshObstacle>();
    }

    public virtual void OnPickedUp(Player player, bool instant = false) {
        this.OnDefocused();

        this.rb.mass = GameParams.Main.movableMass;
        this.rb.velocity = Vector3.zero;
        this.playersCarrying.Add(player);
        this.col.material = GameParams.Main.icePhysicsMaterial;

        if (playersCarrying.Count == 1) {
            SwitchEffects();

            Vector3 newPos = playersCarrying[0].SnapToPlayer(this, detectionPoint);
            float t = 0;
            if (instant == false)
                t = .1f;
            fTransform.DOMove(newPos, t, false).OnComplete(() => {
                player.carryingFurniture = this;
                player.CreateJoints(this.rb);
                this.UpdatePlayersCarrying();
            });
        }
        else if (playersCarrying.Count > 1) {
            player.carryingFurniture = this;
            player.CreateJoints(this.rb);
            this.UpdatePlayersCarrying();
        }
    }

    public virtual void OnDropped(Player player) {
        player.speedMalus = 0;
        this.rb.velocity = Vector3.zero;
        this.rb.angularVelocity = Vector3.zero;

        this.playersCarrying.Remove(player);
        player.carryingFurniture = null;
        player.DeleteJoints();

        if (this.playersCarrying.Count <= 0) {
            this.SwitchEffects();
            this.rb.mass = baseMass;
            this.col.material = GameParams.Main.furniturePhysicsMaterial;
        }
        else {
            this.UpdatePlayersCarrying();
        }
    }


    public virtual void UpdatePlayersCarrying() {
        float _malus = this.weight;

        //For each players carrying
        for (int i = 0; i < this.playersCarrying.Count; i++) {
            //Modify players speed
            this.playersCarrying[i].speedMalus = _malus / this.playersCarrying.Count;

            //Modify players state
            if (this.playersCarrying.Count == 1) {
                this.playersCarrying[i].playerState = CustomEnums.PlayerState.carrying;
                this.playersCarrying[i].UpdateJoints(true);
            }
            else {
                this.playersCarrying[i].playerState = CustomEnums.PlayerState.carryingWithOthers;
                this.playersCarrying[i].UpdateJoints(false);
            }
        }
    }

    public virtual void PutInCar() {
        int nbrPlayersCarrying = playersCarrying.Count;
        for (int i = playersCarrying.Count - 1; i >= 0; i--) {
            //Score
            int tempScore = 0;
            tempScore = Mathf.RoundToInt(this.price / nbrPlayersCarrying);
            this.playersCarrying[i].Score += tempScore;
            this.playersCarrying[i].stats.furnituresSold++;
            if (this.price > 1000)
                playersCarrying[i].stats.expensiveFurnituresSold++;

            //Change state
            this.playersCarrying[i].DeleteJoints();
            this.playersCarrying[i].speedMalus = 0;
            this.playersCarrying[i].playerState = CustomEnums.PlayerState.normal;
            this.playersCarrying[i].animator.SetBool("carrying", false);

            this.playersCarrying[i].carryingFurniture = null;

            this.playersCarrying[i].ResetCarryingPoint();

            this.playersCarrying.RemoveAt(i);
        }

        if (!specialEffects.IsNullOrEmpty()) {
            for (int i = 0; i < specialEffects.Length; i++) {
                if (specialEffects[i].GetComponent<ParticleSystem>() == true)
                    this.DestroySpecialEffect(specialEffects[i]);
            }
        }

        if (GameManager.Main.gameState != CustomEnums.GameState.Menu) {
            GameManager.Main.currentSession.allFurnitures.Remove(this);
            Destroy(this.gameObject);
        }
    }

    public void DestroySpecialEffect(GameObject effect) {
        ParticleSystem.EmissionModule em = effect.GetComponent<ParticleSystem>().emission;
        em.rateOverTime = new ParticleSystem.MinMaxCurve(0, 0);
        effect.transform.parent = null;
        Destroy(effect, 5.0f);
    }

    public virtual void OnFocused(Player player) {
        if (this.outline != null)
            this.outline.OutlineColor = new Color(player.playerMat.color.r, player.playerMat.color.g, player.playerMat.color.b, 1);
    }
    public virtual void OnDefocused() {
        if (this.outline != null)
            this.outline.OutlineColor = new Color(outline.OutlineColor.r, outline.OutlineColor.g, outline.OutlineColor.b, 0);
    }

    public virtual void SwitchEffects() {
        if (!this.specialEffects.IsNullOrEmpty()) {
            for (int i = 0; i < this.specialEffects.Length; i++) {
                this.specialEffects[i].SetActive(!this.specialEffects[i].IsActive());
            }
        }
    }
}

[System.Serializable]
public class FurnitureData {
    public CustomEnums.WeightEnum furnitureWeight;
    public CustomEnums.PriceValue furnitureValue;
}