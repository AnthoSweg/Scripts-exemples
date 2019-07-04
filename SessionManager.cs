using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class SessionManager : MonoBehaviour {
    private static SessionManager _main = null;
    public static SessionManager Main {
        get {
            if (SessionManager._main == null) {
                SessionManager._main = FindObjectOfType<SessionManager>();
            }
            return SessionManager._main;
        }
    }

    [Header("Session Params")]
    public Vector3 SpawnPoint;
    public Vector3 cameraStartPos;
    public AudioClip levelMusic;
    //Used to make only some objects on the list (legendarys) appear
    public List<GameObject> randomObjects = new List<GameObject>();
    public int numberOfObjectsToMakeAppear = 3;

    [Header("Links")]
    public AudioSource ambiantSound;
    public Alarma[] alarmas;

    [HideInInspector]
    public bool sessionHasStarted = false;
    [HideInInspector]
    public float timeLeft;
    [HideInInspector]
    public Player leader = null;
    private float countdown;
    [Header("Debug")]
    public CustomEnums.SessionState sessionState;
    public List<Furniture> allFurnitures = new List<Furniture>();

    [HideInInspector]
    public bool init;

    public async void Initialize() {
        init = true;
        sessionState = CustomEnums.SessionState.None;
        
        GameParams.Main.sessionCam.transform.position = cameraStartPos;
        GameParams.Main.sessionCam.enabled = false;
        MultipleTargetCamera.targets.Clear();
        for (int i = 0; i < GameManager.Main.playersInGame.Count; i++) {
            MultipleTargetCamera.targets.Add(GameManager.Main.playersInGame[i].transform);
        }

        //Delay initialization to avoid freeze during loading
        allFurnitures = FindObjectsOfType<Furniture>().ToList();
        for (int i = 0; i < allFurnitures.Count; i++) {
            allFurnitures[i].Initialize();
            await Task.Delay(1);
        }

        for (int i = 0; i < GameManager.Main.playersInGame.Count; i++) {
            GameManager.Main.playersInGame[i].Drop();
        }
        timeLeft = GameParams.Main.sessionTimeInMinute * 60 + 1;
        timerBumpOnce = false;
        MakeRandomObjectsAppear(this.numberOfObjectsToMakeAppear);

        if (GameParams.Main.TESTSCENE) {
            countdown = 0;
            GameParams.Main.sessionCam.enabled = true;
        }
        else {
            countdown = 5;
            GameParams.Main.timerText.text = "5";
        }
        init = false;
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(this.SpawnPoint, 2);
    }

    public void StartSession() {
        GameManager.Main.gameState = CustomEnums.GameState.InSession;
        if (!GameParams.Main.TESTSCENE)
            sessionState = CustomEnums.SessionState.Countdown;
        else
            sessionState = CustomEnums.SessionState.NormalGame;

    }

    private void Update() {
        if (!GameParams.Main.TESTSCENE)
            UpdateTimer();
    }

    private bool timerBumpOnce;
    private Color blue = Color.white;
    private Color red = Color.white;
    private void UpdateTimer() {
        //Game timer
        if (sessionState == CustomEnums.SessionState.NormalGame || sessionState == CustomEnums.SessionState.Overtime) {
            timeLeft -= Time.deltaTime;
            if (timeLeft < 0)
                timeLeft = 0;

            GameParams.Main.timerText.text = string.Format("{0:0}:{1:00}", Mathf.FloorToInt(timeLeft / 60), Mathf.FloorToInt(timeLeft % 60));

            if (timeLeft < GameParams.Main.overtimeInMinute * 60) {
                OverTime();
                GameParams.Main.timerText.fontSize = GameParams.Main.defaultTimerFontSize * 2 - timeLeft * 2;
                t += Time.deltaTime / (GameParams.Main.overtimeInMinute * 60);
                blue = Color.Lerp(Color.white, new Color(38f / 255f, 125f / 255f, 193f / 255f, 1), t);
                red = Color.Lerp(Color.white, new Color(229f / 255f, 51f / 255f, 57f / 255f, 1), t);

                if (Mathf.FloorToInt(timeLeft) % 2 == 0) {
                    GameParams.Main.timerText.color = blue;
                }
                else {
                    GameParams.Main.timerText.color = red;
                }
            }

            if(Mathf.FloorToInt(timeLeft) == 10 && timerBumpOnce == false) {
                timerBumpOnce = true;
                GameParams.Main.timerBump.SetTrigger("bump");
            }

            if (timeLeft <= 0) {
                GameOver();
            }
        }

        //3 2 1 GO!
        else if (sessionState == CustomEnums.SessionState.Countdown) {
            countdown -= Time.deltaTime;
            GameParams.Main.timerText.text = Mathf.FloorToInt(countdown).ToString();
            GameParams.Main.timerText.fontSize = GameParams.Main.defaultTimerFontSize * 2 + ((5 - Mathf.FloorToInt(countdown)) * 4);
            if (Mathf.FloorToInt(countdown) == 0) {
                GameParams.Main.timerText.text = "STEAL EVERYTHING!";
            }
            if (Mathf.FloorToInt(countdown) == 1) {
                GameParams.Main.PlayersReminder.Hide();
                GameParams.Main.dsd.Show();
                GameParams.Main.sessionCam.enabled = true;
                GameParams.Main.sessionCam.smoothTime *= 1.2f;
                GameParams.Main.eventReminder.Hide();
            }

            if (countdown <= 0) {
                sessionState = CustomEnums.SessionState.NormalGame;
                GameParams.Main.timerText.fontSize = GameParams.Main.defaultTimerFontSize;
                GameParams.Main.sessionCam.smoothTime /= 1.2f;
                KeepTrackOfScores();
                GameParams.Main.sellZoneDisplayer.DisplayZones();
                //GameParams.Main.sessionCam.SetAllPaddings(10);
            }
        }
    }

    private float t;
    public async void OverTime() {
        if (sessionState == CustomEnums.SessionState.Overtime)
            return;

        sessionState = CustomEnums.SessionState.Overtime;

        GameParams.Main.dsd.Hide();

        GameParams.Main.bigInfoText.SetText(string.Format("FASTER FASTER FASTER !"), Color.white);

        t = 0;

        float oSpeed = GameParams.Main.playersSpeed;
        GameParams.Main.playersSpeed *= 1.2f;
        for (int i = 0; i < GameManager.Main.playersInGame.Count; i++) {
            GameManager.Main.playersInGame[i].UnHighlightPlayer();
        }

        GameParams.Main.MainMusic.DOPitch(1.2f, .1f);
        ambiantSound.clip = GameParams.Main.talky;
        ambiantSound.loop = false;
        ambiantSound.Play();

        for (int i = 0; i < alarmas.Length; i++) {
            alarmas[i].OverTime();
        }

        await Task.Delay(TimeSpan.FromSeconds(1.25f));
        //await Task.Delay(TimeSpan.FromSeconds(.6f));

        ambiantSound.clip = GameParams.Main.policeSound;
        ambiantSound.loop = true;
        ambiantSound.volume = .2f;
        ambiantSound.Play();
        ambiantSound.DOFade(1, GameParams.Main.overtimeInMinute * 60).SetEase(Ease.InQuart);

        while (sessionState == CustomEnums.SessionState.Overtime) {

            await Task.Delay(1);

#if UNITY_EDITOR
            if (EditorApplication.isPlaying == false)
                break;
#endif
        }
        GameParams.Main.playersSpeed = oSpeed;
        
        GameParams.Main.MainMusic.pitch = 1;
        GameParams.Main.MainMusic.Stop();
    }

    public async void GameOver() {
        sessionState = CustomEnums.SessionState.GameOver;
        for (int i = 0; i < GameManager.Main.playersInGame.Count; i++) {
            GameManager.Main.playersInGame[i].Drop();
        }

        ambiantSound.clip = GameParams.Main.whistle;
        ambiantSound.loop = false;
        ambiantSound.Play();

        GameParams.Main.timerText.color = Color.white;
        GameParams.Main.timerText.text = "ROUND OVER !";
        GameParams.Main.sellZoneDisplayer.RemoveZones();

        await Task.Delay(TimeSpan.FromSeconds(2));

        GameManager.Main.gameState = CustomEnums.GameState.Loading;

        //red.gameObject.SetActive(false);
        //blue.gameObject.SetActive(false);

        //Fade in
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameParams.Main.SwitchScreen.Show();
        await Task.Delay(TimeSpan.FromSeconds(1));

        int bestScore = 0;
        for (int i = 0; i < GameManager.Main.playersInGame.Count; i++) {
            if (GameManager.Main.playersInGame[i].Score > bestScore) {
                bestScore = GameManager.Main.playersInGame[i].Score;
                leader = GameManager.Main.playersInGame[i];
            }
        }
        //GameParams.Main.UIScore.SetActive(true);
        GameParams.Main.UIInSession.SetActive(false);
        Instantiate(GameParams.Main.GraphsScreen, new Vector3(1000, 0, 1000), Quaternion.identity);
        GameParams.Main.sessionCam.gameObject.SetActive(false);
        //Fade out
        GameParams.Main.SwitchScreen.Hide();
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameManager.Main.gameState = CustomEnums.GameState.AfterSession;
    }

    public void UpdateScore() {
        Player tempLeader = null;
        int tempBestScore = 0;

        for (int i = 0; i < GameManager.Main.playersInGame.Count; i++) {
            if (GameManager.Main.playersInGame[i].Score >= tempBestScore) {
                tempBestScore = GameManager.Main.playersInGame[i].Score;
                tempLeader = GameManager.Main.playersInGame[i];
            }
        }

        if (this.sessionState != CustomEnums.SessionState.Overtime) {
            //First time
            if (leader == null) {
                leader = tempLeader;
                tempLeader.HighlightPlayer();
                DisplayNewWinner(tempLeader);
            }

            if (leader != tempLeader) {
                leader.UnHighlightPlayer();
                tempLeader.HighlightPlayer();

                DisplayNewWinner(tempLeader);
            }
            GameParams.Main.dsd.UpdatePlaces();
        }

        leader = tempLeader;

    }

    private void MakeRandomObjectsAppear(int nbrToAppear) {
        if (!randomObjects.IsNullOrEmpty()) {
            for (int i = 0; i < nbrToAppear; i++) {
                randomObjects.RemoveAt(UnityEngine.Random.Range(0, randomObjects.Count));
            }

            for (int i = 0; i < randomObjects.Count; i++) {
                if (!randomObjects[i].IsNullOrInactive())
                    randomObjects[i].SetActive(false);
            }
        }
    }

    public void DisplayNewWinner(Player player) {
        if (this.sessionState != CustomEnums.SessionState.Overtime) {
            GameParams.Main.bigInfoText.SetText(string.Format("{0} {1} !", GameParams.Main.newWinnerMessage, player.playerName), player.playerMat.color);
        }
    }

    private async void KeepTrackOfScores() {
        while (this.sessionState != CustomEnums.SessionState.GameOver) {
            for (int i = 0; i < GameManager.Main.playersInGame.Count; i++) {
                GameManager.Main.playersInGame[i].scoreTracker.Add(GameManager.Main.playersInGame[i].Score);
            }
            await Task.Delay(TimeSpan.FromSeconds((GameParams.Main.sessionTimeInMinute * 60) / GameParams.Main.numberOfScoreTracking));
        }
        for (int i = 0; i < GameManager.Main.playersInGame.Count; i++) {
            GameManager.Main.playersInGame[i].scoreTracker.Add(GameManager.Main.playersInGame[i].Score);
        }
    }
}

