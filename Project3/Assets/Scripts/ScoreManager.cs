using HighScore;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreManager : MonoBehaviour
{
    public int score;
    public TMP_InputField playerNameInput;
    public TextMeshProUGUI scoreErrorDisplay;
    public GameObject winPanel;

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
        winPanel.SetActive(false);
    }

    public void WonGame()
    {
        if (Journal.Instance != null)
        {
            Journal.Instance.WonGameMenu();
        }
    }

    public void CalculateScore()
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
        //score = (int)(collectibleBonus * diffMult);
        score = 67_420;
    }

    public void SubmitScore()
    {
        if (string.IsNullOrWhiteSpace(playerNameInput.text) || score == 0)
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