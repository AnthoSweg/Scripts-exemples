using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour {
    private static GameManager _main = null;
    public static GameManager Main {
        get {
            if (GameManager._main == null) {
                GameManager._main = FindObjectOfType<GameManager>();
            }
            return GameManager._main;
        }
    }

    public List<colorSwapZone> matsAvailable = new List<colorSwapZone>();
    public List<Player> playersInGame = new List<Player>();

    public CustomEnums.GameState gameState;
    public SessionManager currentSession;

    private List<GameObject> levelsAvailable = new List<GameObject>();
    [HideInInspector]
    public int numberOfRoundsPlayed = 0;

    private void Awake() {
        StartGame();
    }

    public void StartGame() {
        Cursor.visible = false;
        /////Set up menu
        this.gameState = CustomEnums.GameState.Menu;
        colorSwapZone[] csz = FindObjectsOfType<colorSwapZone>();
        for (int i = 0; i < csz.Length; i++) {
            matsAvailable.Add(csz[i]);
        }

        //used to initialize the buttons in menu
        Furniture[] furs = FindObjectsOfType<Furniture>();
        List<GameObject> go = new List<GameObject>();
        for (int i = 0; i < furs.Length; i++) {
            if (furs[i].GetComponent<StartButton>() != null || furs[i].GetComponent<CreditsButton>() != null || furs[i].GetComponent<QuitButton>() != null || furs[i].GetComponent<Boss>() != null) {
                furs[i].Initialize();
            }
            else {
                go.Add(furs[i].gameObject);
            }
        }

        if (!GameParams.Main.TESTSCENE) {
            for (int i = 0; i < go.Count; i++) {
                Furniture f = go[i].GetComponent<Furniture>();
                var mf = go[i].gameObject.AddComponent<MenuFurniture>();
                mf.data = f.data;
                if (!f.specialEffects.IsNullOrEmpty())
                    mf.specialEffects = (GameObject[])f.specialEffects.Clone();

                if (f != null)
                    Destroy(f);

                mf.Initialize();
            }

            GameManager.Main.numberOfRoundsPlayed = 0;
            GameParams.Main.MainMusic.clip = GameParams.Main.MenuMusic;
            GameParams.Main.MainMusic.Play();
            GameParams.Main.UIMenu.SetActive(true);
            GameParams.Main.UIInSession.SetActive(false);
            GameParams.Main.UIInstructions.SetActive(false);
            GameParams.Main.menuCam.gameObject.SetActive(true);
            GameParams.Main.sessionCam.gameObject.SetActive(false);
            GameParams.Main.medalsDisplayer.SetActive(false);
            GameParams.Main.medalsDisplayer.SetActive(false);
        }
        else {
            GameManager.Main.TestScene();
        }
    }

    public async void BeginSession() {
        for (int i = 0; i < playersInGame.Count; i++) {
            playersInGame[i].Drop();
            playersInGame[i].Score = 0;
            playersInGame[i].scoreTracker.Clear();
            if (this.gameState != CustomEnums.GameState.Menu) {
                playersInGame[i].gameObject.SetActive(false);
            }
            else
                playersInGame[i].buttonA.SetActive(false);
        }

        GameParams.Main.MainMusic.Stop();
        gameState = CustomEnums.GameState.Loading;

        //Fade in
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameParams.Main.SwitchScreen.Show();
        await Task.Delay(TimeSpan.FromSeconds(1));

        //Setup
        GameParams.Main.UIInstructions.SetActive(true);
        GameParams.Main.menuCam.gameObject.SetActive(false);
        GameParams.Main.loadingScene.SetActive(true);
        GameParams.Main.medalsDisplayer.SetActive(false);
        GameParams.Main.UIMenu.SetActive(false);
        if (GameParams.Main.Menu != null)
            GameParams.Main.Menu.SetActive(false);

        //Fade out
        GameParams.Main.SwitchScreen.Hide();
        await Task.Delay(TimeSpan.FromSeconds(1));

        GameParams.Main.PlayersReminder.Show();


        //Setup the new session
        GameManager.Main.currentSession = Instantiate(DetermineLevelToLaunch(), Vector3.zero, Quaternion.identity).GetComponent<SessionManager>();
        GameManager.Main.currentSession.Initialize();
        
        for (int i = 0; i < playersInGame.Count; i++) {
            GameManager.Main.playersInGame[i].cam = GameParams.Main.sessionCam.cam;
            GameManager.Main.playersInGame[i].transform.position = StaticUtils.RandomCircle(currentSession.SpawnPoint, 2);
            GameManager.Main.playersInGame[i].playerNumberText.gameObject.SetActive(false);
            GameManager.Main.playersInGame[i].gameObject.SetActive(true);
        }

        //Make sure the loading lasts at least 5s
        float loadTime = 0;
        while (currentSession.init == true) {
            loadTime += Time.deltaTime;
            await Task.Delay(10);
        }
        if(loadTime < 5)
            await Task.Delay(TimeSpan.FromSeconds(5 - loadTime));

        //Fade in
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameParams.Main.SwitchScreen.Show();
        await Task.Delay(TimeSpan.FromSeconds(1));

        GameParams.Main.UIInstructions.SetActive(false);
        GameParams.Main.loadingScene.SetActive(false);
        GameParams.Main.sessionCam.gameObject.SetActive(true);
        GameParams.Main.UIInSession.SetActive(true);

        //Fade out
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameParams.Main.SwitchScreen.Hide();
        await Task.Delay(TimeSpan.FromSeconds(1));

        GameParams.Main.MainMusic.clip = currentSession.levelMusic;
        GameParams.Main.MainMusic.Play();

        GameManager.Main.currentSession.StartSession();
        GameParams.Main.eventReminder.Show();

        if (GameManager.Main.numberOfRoundsPlayed == 1 && GameParams.Main.Menu != null)
            Destroy(GameParams.Main.Menu);
    }

    float fpsUpdateDelay = .5f;
    float fpsTime = 0f;
    int FPSCounter = 0;
    private void Update() {
        //Hack to relaunch scene
        if (Input.GetKeyDown(KeyCode.R)) {
            LoadMenu();
        }

        //Display FPS
        if (GameParams.Main.fpsText != null) {
            FPSCounter++;
            if (fpsTime + fpsUpdateDelay < Time.unscaledTime) {
                FPSCounter = Mathf.RoundToInt(1f * FPSCounter / (Time.unscaledTime - fpsTime));
                GameParams.Main.fpsText.text = FPSCounter.ToString();

                fpsTime = Time.unscaledTime;
                FPSCounter = 0;
            }
        }

        ////CHECK PAUSE -- ABORTED
        //for (int i = 0; i < GameManager.Main.playersInGame.Count; i++)
        //{
        //    if (GameManager.Main.playersInGame[i].Input.StartIsPressed)
        //    {
        //        if(gameState == CustomEnums.GameState.Pause)
        //        {
        //            Debug.Log("UNPAUSED");
        //            gameState = CustomEnums.GameState.InSession;
        //            Time.timeScale = 1;
        //        break;
        //        }
        //        else if (gameState == CustomEnums.GameState.InSession)
        //        {
        //            Debug.Log("PAUSED");
        //            gameState = CustomEnums.GameState.Pause;
        //            Time.timeScale = 1;
        //            Time.fixedDeltaTime = 0;
        //            break;
        //        }
        //    }
        //}
    }

    public void TestScene() {
        gameState = CustomEnums.GameState.InSession;
        currentSession.Initialize();
        currentSession.StartSession();
    }

    public void LoadMenu() {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public async void QuitGame() {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif

        //Fade in
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameParams.Main.SwitchScreen.Show();
        await Task.Delay(TimeSpan.FromSeconds(1));

        Application.Quit();
    }    

    public GameObject DetermineLevelToLaunch() {
        numberOfRoundsPlayed++;

        if (GameParams.Main.testLevel != null)
            return GameParams.Main.testLevel;

        if (GameParams.Main.playLevelsRandomly) {
            if (levelsAvailable.IsNullOrEmpty())
                levelsAvailable = GameParams.Main.levels.ToList();

            GameObject lvl = levelsAvailable[UnityEngine.Random.Range(0, levelsAvailable.Count)];
            levelsAvailable.Remove(lvl);

            return lvl;
        }
        else
            return GameParams.Main.levels[numberOfRoundsPlayed - 1];

    }

    public int GetHighsetOverallScore() {
        int maxScore = 0;
        for (int i = 0; i < playersInGame.Count; i++) {
            if (playersInGame[i].overallScore > maxScore)
                maxScore = playersInGame[i].overallScore;
        }
        return maxScore;
    }

    public async void ShowPodium() {
        //Fade in
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameParams.Main.SwitchScreen.Show();
        await Task.Delay(TimeSpan.FromSeconds(1));

        if (currentSession != null) {
            Destroy(currentSession.gameObject);
        }
        Instantiate(GameParams.Main.podium);

        //Fade out
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameParams.Main.SwitchScreen.Hide();
        await Task.Delay(TimeSpan.FromSeconds(1));
        GameParams.Main.MainMusic.clip = GameParams.Main.PodiumMusic;
        GameParams.Main.MainMusic.loop = true;
        GameParams.Main.MainMusic.Play();
        await Task.Delay(TimeSpan.FromSeconds(6));
        GameParams.Main.medalsDisplayer.SetActive(true);
    }

    public IEnumerator Credits() {
        GameParams.Main.CreditsUI.SetActive(true);
        GameManager.Main.gameState = CustomEnums.GameState.Pause;
        yield return new WaitForSeconds(15f);
        GameParams.Main.CreditsUI.SetActive(false);
        GameManager.Main.gameState = CustomEnums.GameState.Menu;
    }
}