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
    public TextMeshProUGUI waveTimerText; // "Next wave in X..." countdown

    [Header("Pause")]
    public GameObject pausePanel;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI finalWaveText;

    [HideInInspector] public bool isGameOver;
    [HideInInspector] public bool isPaused;

    private int score;
    private int currentWave = 1;
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
    }

    void Update()
    {
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
        UpdateHUD();
    }

    // called when an enemy reaches the player's goal
    public void EnemyReachedPlayerGoal()
    {
        lives--;
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

    // hooked to Quit button onClick
    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
