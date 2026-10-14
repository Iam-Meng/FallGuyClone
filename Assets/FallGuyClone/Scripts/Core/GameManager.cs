using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FallGuyClone
{
    public enum GameState { Menu, Countdown, Playing, Paused, Qualified, Eliminated }

    /// <summary>Round flow: menu, countdown, timer, checkpoints, respawn, qualify / eliminate.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Set before reloading the scene to skip the main menu (Retry).</summary>
        static bool skipMenuOnLoad;

        const string BestTimeKey = "FallGuyClone_BestTime";
        const string SkinKey = "FallGuyClone_Skin";
        const float HighFlightHeight = 24f;
        const float HighFlightDuration = 2.5f;

        public GameState State { get; private set; }
        public float TimeLimit { get; private set; }
        public float TimeLeft { get; private set; }
        public float Elapsed { get; private set; }
        public int CheckpointIndex { get; private set; }
        public int CheckpointCount { get; private set; }
        public int Falls { get; private set; }
        public int SkinIndex { get; private set; }
        public float Progress => player == null ? 0f
            : Mathf.Clamp01(Mathf.InverseLerp(course.startZ, course.finishZ, player.transform.position.z));
        public float BestTime => PlayerPrefs.GetFloat(BestTimeKey, 0f);

        PlayerController player;
        PlayerAvatar avatar;
        ThirdPersonCamera cam;
        GameUI ui;
        GameFeedback feedback;
        Course course;
        Vector3 respawnPoint;
        float countdown;
        float highFlightTimer;
        GameState stateBeforePause;

        public void Init(PlayerController player, PlayerAvatar avatar, ThirdPersonCamera cam, GameUI ui, Course course, float timeLimit)
        {
            Instance = this;
            this.player = player;
            this.avatar = avatar;
            this.cam = cam;
            this.ui = ui;
            this.course = course;
            feedback = gameObject.AddComponent<GameFeedback>();
            feedback.Bind(player);
            TimeLimit = timeLimit;
            TimeLeft = timeLimit;
            CheckpointCount = course.checkpoints.Count;
            respawnPoint = course.spawnPoint;
            Time.timeScale = 1f;

            SkinIndex = PlayerPrefs.GetInt(SkinKey, 0);
            avatar.Build(SkinIndex);

            if (skipMenuOnLoad)
            {
                skipMenuOnLoad = false;
                StartCountdown();
            }
            else
            {
                SetState(GameState.Menu);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Flow ----------

        public void StartCountdown()
        {
            countdown = 3f;
            TimeLeft = TimeLimit;
            Elapsed = 0f;
            highFlightTimer = 0f;
            SetState(GameState.Countdown);
        }

        public void TogglePause()
        {
            if (State == GameState.Paused)
            {
                Time.timeScale = 1f;
                SetState(stateBeforePause);
            }
            else if (State == GameState.Playing || State == GameState.Countdown)
            {
                stateBeforePause = State;
                Time.timeScale = 0f;
                SetState(GameState.Paused);
            }
        }

        public void Retry()
        {
            skipMenuOnLoad = true;
            Reload();
        }

        public void BackToMenu()
        {
            skipMenuOnLoad = false;
            Reload();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ChangeSkin(int delta)
        {
            int n = PlayerAvatar.Skins.Length;
            SkinIndex = ((SkinIndex + delta) % n + n) % n;
            PlayerPrefs.SetInt(SkinKey, SkinIndex);
            avatar.Build(SkinIndex);
            ui.Refresh();
        }

        static void Reload()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().buildIndex : 0);
        }

        public void ReachCheckpoint(Checkpoint cp)
        {
            if (State != GameState.Playing || cp.index <= CheckpointIndex) return;
            CheckpointIndex = cp.index;
            respawnPoint = cp.respawnPoint;
            ui.Toast($"CHECKPOINT {cp.index}/{CheckpointCount}", Art.Cyan);
        }

        public void Finish()
        {
            if (State != GameState.Playing) return;
            bool newBest = BestTime <= 0f || Elapsed < BestTime;
            if (newBest)
            {
                PlayerPrefs.SetFloat(BestTimeKey, Elapsed);
                PlayerPrefs.Save();
            }
            avatar.ForcedPose = PlayerAvatar.Pose.Win;
            ui.SetResult(true, newBest);
            SetState(GameState.Qualified);
        }

        void Eliminate()
        {
            TimeLeft = 0f;
            avatar.ForcedPose = PlayerAvatar.Pose.Lose;
            ShowCatReaction(persistent: true);
            ui.SetResult(false, false);
            SetState(GameState.Eliminated);
        }

        void ShowCatReaction(float duration = 0f, bool persistent = false)
        {
            ui.ShowCat(duration, persistent);
            feedback.PlayCatLaugh();
        }

        void SetState(GameState s)
        {
            State = s;
            bool playing = s == GameState.Playing;
            player.ControlEnabled = playing;
            cam.InputEnabled = playing || s == GameState.Countdown;
            bool lockCursor = s == GameState.Playing || s == GameState.Countdown;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
            ui.ShowState(s);
        }

        // ---------- Update ----------

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) TogglePause();
                if (State == GameState.Menu)
                {
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) StartCountdown();
                    if (kb.qKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) ChangeSkin(-1);
                    if (kb.eKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) ChangeSkin(1);
                }
                else if (State == GameState.Qualified || State == GameState.Eliminated)
                {
                    if (kb.enterKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame) Retry();
                }
                else if (State == GameState.Playing && kb.rKey.wasPressedThisFrame)
                {
                    RespawnPlayer("RESPAWN");
                }
            }

            // Clicking the game view re-locks the cursor (e.g. after alt-tab).
            var mouse = Mouse.current;
            if ((State == GameState.Playing || State == GameState.Countdown) && mouse != null &&
                mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            switch (State)
            {
                case GameState.Countdown:
                    float before = countdown;
                    countdown -= Time.deltaTime;
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(countdown) && countdown > 0f)
                        ui.BigMessage(Mathf.CeilToInt(countdown).ToString(), Art.Yellow);
                    if (before == 3f) ui.BigMessage("3", Art.Yellow);
                    if (countdown <= 0f)
                    {
                        ui.BigMessage("GO!", Art.Mint);
                        SetState(GameState.Playing);
                    }
                    break;

                case GameState.Playing:
                    TimeLeft -= Time.deltaTime;
                    Elapsed += Time.deltaTime;
                    if (TimeLeft <= 0f)
                    {
                        Eliminate();
                        break;
                    }

                    if (!player.IsGrounded && player.transform.position.y >= HighFlightHeight)
                    {
                        highFlightTimer += Time.deltaTime;
                        if (highFlightTimer >= HighFlightDuration)
                        {
                            Falls++;
                            ShowCatReaction(2.5f);
                            RespawnPlayer("TOO HIGH!");
                        }
                    }
                    else highFlightTimer = 0f;
                    break;
            }

            if (player.transform.position.y < course.killY && State != GameState.Paused)
            {
                if (State == GameState.Playing)
                {
                    ShowCatReaction(2.5f);
                }
                RespawnPlayer(State == GameState.Playing ? "OOPS!" : null);
            }
        }

        void RespawnPlayer(string message)
        {
            if (message == "OOPS!") Falls++;
            highFlightTimer = 0f;
            player.Teleport(respawnPoint, Quaternion.identity);
            cam.yaw = 0f;
            cam.SnapBehindTarget();
            if (!string.IsNullOrEmpty(message)) ui.Toast(message, Art.Pink);
        }
    }
}
