﻿using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelInfo : MonoBehaviour
{
    public string levelName;
    public string levelAuthor;
    public string levelVersion;

    [Tooltip("Parent object of spawnpoint objects.")]
    public GameObject spawnPointParent;

    [Tooltip("Spectator camera of the level. Optional.")]
    public Camera spectatorCamera;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(levelName))
            throw new ArgumentException("Level Name must not be empty");
    }

    private void Start()
    {
        if (SceneManager.sceneCount > 1 && spectatorCamera)
            spectatorCamera.gameObject.SetActive(false);
    }
}