using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;

public class PlyDownloader : MonoBehaviour
{
    #region public variables
    public Text debug;
    public string plyURL = "http://www.julianrutten.com/Data/Tree.ply";
    public float fps;
    public rendererType selectedRenderer;
    public float pointSize=0.01f;
    #endregion

    #region private variables
    PlyPointCloudData plyDown = null;

    object typelessRenderer;
    PCXPointCloudRenderer pcxRenderer;
    ParticleSystem cpuRenderer;
    VisualEffect vfxRenderer;

    public bool isSetup = false;
    float deltaTime;
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        vfxRenderer = gameObject.GetComponent<VisualEffect>();
        pcxRenderer = gameObject.GetComponent<PCXPointCloudRenderer>();
        cpuRenderer = gameObject.GetComponent<ParticleSystem>();

        if (vfxRenderer != null)
        {
            selectedRenderer = rendererType.GPU_VFX;
            plyDown = new PlyPointCloudData(selectedRenderer);
            typelessRenderer = vfxRenderer;
        }
        
        if (cpuRenderer != null)
        {
            selectedRenderer = rendererType.CPU_Shuriken;
            plyDown = new PlyPointCloudData(selectedRenderer);
            typelessRenderer = cpuRenderer;
        }
        if (pcxRenderer != null)
        {
            selectedRenderer = rendererType.GPU_PCX;
            plyDown = new PlyPointCloudData(selectedRenderer);
            typelessRenderer = pcxRenderer;
        }

        if (plyDown != null)
        {
            plyDown.setPointSize = pointSize;
            StartCoroutine(plyDown.GetRequest(@plyURL));
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        fps = 1.0f / deltaTime;
        if (debug != null)
        {
            if (plyDown != null)
            {
                debug.text = Mathf.Ceil(fps).ToString() + plyDown.debugLog;
            }
            else debug.text = Mathf.Ceil(fps).ToString();
        }
        if (plyDown != null && !isSetup && plyDown.isFinished)
        {
            isSetup = true;
            plyDown.updateRenderer(typelessRenderer);
        }
    }
}
