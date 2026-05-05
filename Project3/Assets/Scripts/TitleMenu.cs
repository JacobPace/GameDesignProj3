using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class TitleMenu : MonoBehaviour
{
    [SerializeField] private GameObject[] menus;

    public void ToggleMenu(int menuIndex) => menus[menuIndex].SetActive(!menus[menuIndex].activeSelf);
   
    public void HandleDifficultyChange(int index)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDifficulty(index);
        }
    }

    public void StartGame() => SceneManager.LoadScene("MapTestScene");
    public void QuitGame() => Application.Quit();

}
