using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum Difficulty { Easy, Normal, Hard }
    public Difficulty currentDifficulty = Difficulty.Normal;

    [SerializeField] GameObject[] ResourceStations;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persists across scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetDifficulty(int index)
    {
        currentDifficulty = (Difficulty)index;
        Debug.Log("Difficulty set to: " + currentDifficulty);
        
        // More difficulty settings/setup below

    }

}
