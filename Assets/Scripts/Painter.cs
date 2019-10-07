﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineRenderer))]
public class Painter : MonoBehaviour
{
    public float maxInk = 10f;
    private float inkLevel;
    public float threshold = 0.001f;
    public GameObject template;
    public OurNetworkManager network;
    private bool isDragging = false;
    private List<Vector3> lineBuffer;
    private LineRenderer lineRenderer;
    private Camera cam;
    private int lineCount = 0;
    public Text textUI;

    // Start is called before the first frame update

    void Awake()
    {
        this.lineRenderer = this.GetComponent<LineRenderer>();
        this.cam = Camera.main;
        inkLevel = maxInk;
    }

    // Update is called once per frame
    void Update()
    {
        textUI.text = inkLevel <= 0 ? "0" : Math.Round(inkLevel, 2).ToString();

        if (Input.GetMouseButtonDown(0)) {
            this.isDragging = true;
            this.lineBuffer = new List<Vector3>();
            this.lineCount = 0;
        }
        if (Input.GetMouseButtonUp(0)) {
            this.isDragging = false;
            // Add the element to the screen
            this.AddLine();
            this.lineBuffer = new List<Vector3>();
            this.lineCount = 0;
            this.lineRenderer.positionCount = 0;
        }

        if (Input.GetKeyDown(KeyCode.Space)) {
            Debug.Log("Deleting all the things!");
            var toDelete = GameObject.FindGameObjectsWithTag("dynamic");
            Debug.Log($"Deleting {toDelete.Length} Objects");
            foreach (var obj in toDelete)
            {
                Destroy(obj);
            }
            inkLevel = maxInk;
            network.ResetLevel();
        }
        
        
        if (this.isDragging && this.inkLevel >= 0) {
            this.Drag();
        }
    }

    void Drag() {
        var mousePos = Input.mousePosition;
        mousePos.z = cam.nearClipPlane;
        var mouseWorld = cam.ScreenToWorldPoint(mousePos);
        mouseWorld.z = -1;

        if (lineBuffer.Count == 0 || Vector2.Distance(mouseWorld, lineBuffer[lineBuffer.Count - 1]) > this.threshold) {
            lineBuffer.Add(mouseWorld);
            this.UpdateLine();
        }
    }

    void UpdateLine() {
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.positionCount = lineBuffer.Count;
        for (int i = this.lineCount; i < lineBuffer.Count; i++) {
            lineRenderer.SetPosition(i, lineBuffer[i]);
            if (this.lineCount > 1)
            {
                this.inkLevel -= (lineBuffer[i]-lineBuffer[i-1]).magnitude;
            }

        }
        lineCount = lineBuffer.Count;
    }

    void AddLine() {
        this.network.AddLine(this.lineBuffer);
    }
}