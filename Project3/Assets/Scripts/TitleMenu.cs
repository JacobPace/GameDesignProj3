using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class TitleMenu : MonoBehaviour
{
    [SerializeField] private GameObject[] menus;

    public AudioClip gameMusic;

    public void ToggleMenu(int menuIndex) => menus[menuIndex].SetActive(!menus[menuIndex].activeSelf);
   
    public void HandleDifficultyChange(int index)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SetDifficulty(index);
    }

    public void StartGame()
    {
        Time.timeScale = 1.0f;
        MusicManager.Instance.PlayMusic(gameMusic, 1.5f);
        SceneTransitionManager.Instance.LoadScene("MapTestScene");
    }

    public void QuitGame() => Application.Quit();
}
