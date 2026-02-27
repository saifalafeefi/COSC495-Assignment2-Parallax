using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameManagerX : MonoBehaviour
{
    public static GameManagerX Instance;

    [Header("Game Settings")]
    public int lives = 3;

    [Header("HUD")]
    public TextMeshProUGUI livesText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI gameTimerText;
    public TextMeshProUGUI waveTimerText; // "Next wave in X..." countdown

    [Header("Pause")]
    public GameObject pausePanel;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI finalWaveText;
    public TextMeshProUGUI finalTimeText;

    // fired when an enemy is scored (knocked into enemy goal, shield kill, etc.)
    public static event System.Action OnEnemyScored;

    [HideInInspector] public bool isGameOver;
    [HideInInspector] public bool isPaused;

    private int score;
    private int currentWave = 1;
    private float totalGameplayTime;
    private bool isWaveActive;
    private float prePauseTimeScale;
    private RotateCameraX cameraRotator;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        cameraRotator = FindObjectOfType<RotateCameraX>();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        UpdateHUD();
        UpdateGameTimerText();
    }

    void Update()
    {
        // game timer runs only during active waves, and pauses on pause/game over
        if (!isGameOver && !isPaused && isWaveActive)
        {
            totalGameplayTime += Time.unscaledDeltaTime;
            UpdateGameTimerText();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && !isGameOver)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    // called when an enemy is knocked into the enemy goal
    public void EnemyScored()
    {
        score++;
        OnEnemyScored?.Invoke();
        if (SFXManager.Instance != null) SFXManager.Instance.PlayEnemyScore();
        UpdateHUD();
    }

    // called when an enemy reaches the player's goal
    public void EnemyReachedPlayerGoal()
    {
        lives--;
        if (SFXManager.Instance != null) SFXManager.Instance.PlayPlayerGoal();
        if (lives <= 0)
        {
            lives = 0;
            GameOver();
        }
        UpdateHUD();
    }

    // called by SpawnManagerX at the start of each wave
    public void SetWave(int wave)
    {
        currentWave = wave;
        UpdateHUD();
    }

    // called by SpawnManagerX when entering/leaving active combat
    public void SetWaveActive(bool active)
    {
        isWaveActive = active;
    }

    // show/hide the "Next wave in X..." text
    public void SetWaveTimer(string text)
    {
        if (waveTimerText != null)
        {
            waveTimerText.text = text;
            waveTimerText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    void UpdateHUD()
    {
        if (livesText != null) livesText.text = "Lives: " + lives;
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (waveText != null) waveText.text = "Wave: " + currentWave;
    }

    void GameOver()
    {
        isGameOver = true;
        isWaveActive = false;
        Time.timeScale = 0f;

        // unlock cursor so player can click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        if (finalScoreText != null) finalScoreText.text = "Score: " + score;
        if (finalWaveText != null) finalWaveText.text = "Wave: " + currentWave;
        if (finalTimeText != null) finalTimeText.text = "Time: " + FormatTime(totalGameplayTime);
    }

    void PauseGame()
    {
        isPaused = true;
        prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (cameraRotator != null) cameraRotator.Freeze();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    // hooked to Resume button onClick
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = prePauseTimeScale;

        if (cameraRotator != null) cameraRotator.Unfreeze();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    // hooked to Restart button onClick
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // hooked to Main Menu button onClick
    public void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main Menu");
    }

    // hooked to Quit button onClick
    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void UpdateGameTimerText()
    {
        if (gameTimerText != null)
            gameTimerText.text = "Time: " + FormatTime(totalGameplayTime);
    }

    string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        int mins = totalSeconds / 60;
        int secs = totalSeconds % 60;
        return mins.ToString("00") + ":" + secs.ToString("00");
    }
}
