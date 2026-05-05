using UnityEngine;
using UnityEngine.SceneManagement;

public class CanvasController : MonoBehaviour
{
    public GameObject winScreen;

    public static CanvasController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        winScreen.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ShowScreen()
    {
        Time.timeScale = 0f;
        winScreen.SetActive(true);
    }

    public void ReturnToTitle() => SceneManager.LoadScene("TitleScene");
    public void QuitGame() => Application.Quit();
}
