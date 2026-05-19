using HighScore;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreManager : MonoBehaviour
{
    public int score;
    public TMP_InputField playerNameInput;
    public TextMeshProUGUI gameOverDisplay;
    public TextMeshProUGUI scoreDisplay;
    public TextMeshProUGUI scoreErrorDisplay;
    public GameObject screen;
    public GameObject playerUI;

    public static ScoreManager Instance { get; private set; }

    private Coroutine errorCoroutine;

    void Start()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        HS.Init(this, "The Ossuary");
        score = 0;
        playerNameInput.text = "";
        scoreErrorDisplay.gameObject.SetActive(false);
        screen.SetActive(false);
        playerUI.SetActive(true);
    }

    public void EndGame(bool hasEscaped)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Player.Instance._playerInput.SwitchCurrentActionMap("UI");
        score = hasEscaped ? CalculateScore() : (CalculateScore() / 2);
        gameOverDisplay.text = hasEscaped ? "You Escaped!" : "You Died!!!";
        scoreDisplay.text = $"Score: {score}";
        screen.SetActive(true);
        playerUI.SetActive(false);
    }

    public int CalculateScore()
    {
        int collectibleBonus = Player.Instance.inventory.GetCount("Collectible") * 100;
        float diffMult = 1f;
        if (GameManager.Instance != null)
        {
            diffMult = GameManager.Instance.currentDifficulty switch
            {
                GameManager.Difficulty.Easy => 0.75f,
                GameManager.Difficulty.Normal => 1,
                GameManager.Difficulty.Hard => 2,
                _ => 1,
            };
        }
        // time bonus -> max bonus is from completion within 20 mins
        return (int)(collectibleBonus * diffMult);
        //return 67_420;
    }

    public void SubmitScore()
    {
        if (string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            ShowInputError("Please enter a valid name!");
        }
        else
        {
            HS.SubmitHighScore(this, playerNameInput.text, score);
            SceneManager.LoadScene("Title");
        }
    }

    public void ClearScores()
    {
        HS.Clear(this);
    }

    private void ShowInputError(string errorMessage)
    {
        scoreErrorDisplay.text = errorMessage;

        if (errorCoroutine != null)
        {
            StopCoroutine(errorCoroutine);
        }

        errorCoroutine = StartCoroutine(DisplayErrorRoutine(5.0f));
    }

    // Coroutine that handles the visual timer
    private IEnumerator DisplayErrorRoutine(float delay)
    {
        scoreErrorDisplay.gameObject.SetActive(true);
        yield return new WaitForSeconds(delay);
        scoreErrorDisplay.gameObject.SetActive(false);
        errorCoroutine = null;
    }
    public void ReturnToTitle() => SceneManager.LoadScene("Title");
    public void QuitGame() => Application.Quit();
}