
using UnityEngine;
using UnityEngine.Serialization;


[ExecuteInEditMode]
[AddComponentMenu("Post Process/God Rays")]
[RequireComponent(typeof(UnityEngine.Camera))]
public class PostProcessGodRays : MonoBehaviour
{

    public Shader shader;

    [Header("Bloom:")]
    [Range(0.0f, 1.0f)]
    public float godRaysAmount = 0.5f;
    
    [Range(0.0f, 1.0f)]
    public float godRaysThreshold = 0.2f;

    [Range(0.0f, 1.0f)]
    public float godRaysSceneContribution = 1.0f;

    [Range(0.0f, 1.0f)]
    public float godRaysSunContribution = 1.0f;

    [Range(0.0f, 1.0f)] public float godRayEdgeFalloff = 0.15f;

    [Range(0.0f, 1.0f)]
    public float godRayLength = 0.8f;

    public int godRaySteps = 8;

    public bool screenValues = false;

    private Material godRaysMaterial;
    private Camera thisCamera;
    
    private RenderTexture _bloomThresholdTexture;
    private RenderTexture _zoomBlur1;
    private RenderTexture _zoomBlur2;
    private RenderTexture _copy;

    void Awake() {
        OnEnable();
    }

    void Start() {
        OnEnable();
    }

    void OnEnable() {
        if( thisCamera == null){
            thisCamera = GetComponent<Camera>();
        }
        
        thisCamera.depthTextureMode = DepthTextureMode.Depth;

        // Get Shader
        if (shader == null) {
            shader = Shader.Find("Hidden/PostProcess/GodRays");
        }

        if (shader == null) {
            Debug.Log("#ERROR# Hidden/PostProcess Shader not found");
            return;
        }

        // Create Post Process Material
        if (godRaysMaterial == null) {
            godRaysMaterial = new Material(shader);
            godRaysMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
    }
    
    static class Properties {
        public static int _GodRayAmount = Shader.PropertyToID("_GodRayAmount");
        public static int _GodRaySceneContribution = Shader.PropertyToID("_GodRaySceneContribution");
        public static int _GodRaySunContribution = Shader.PropertyToID("_GodRaySunContribution");
        public static int _GodRayThreshold = Shader.PropertyToID("_GodRayThreshold");
        public static int _GodRayLength = Shader.PropertyToID("_GodRayLength");
        public static int _GodRaySteps = Shader.PropertyToID("_GodRaySteps");
        public static int _GodRayEdgeFalloff = Shader.PropertyToID("_GodRayEdgeFalloff");
        
        public static int _GodRayScreenPos = Shader.PropertyToID("_GodRayScreenPos");
        public static int _SunDir = Shader.PropertyToID("_SunDir");
        public static int _SunColor = Shader.PropertyToID("_SunColor");
        public static int _ViewDirTL = Shader.PropertyToID("_ViewDirTL");
        public static int _ViewDirTR = Shader.PropertyToID("_ViewDirTR");
        public static int _ViewDirBL = Shader.PropertyToID("_ViewDirBL");
        public static int _ViewDirBR = Shader.PropertyToID("_ViewDirBR");
        public static int _GodRayTex = Shader.PropertyToID("_GodRayTex");
    }
    
    public void Load(Material material) {
        material.SetFloat(Properties._GodRayAmount, godRaysAmount);
        material.SetFloat(Properties._GodRaySceneContribution, godRaysSceneContribution);
        material.SetFloat(Properties._GodRaySunContribution, godRaysSunContribution);
        material.SetFloat(Properties._GodRayThreshold, godRaysThreshold);
        material.SetFloat(Properties._GodRayLength, godRayLength);
        material.SetFloat(Properties._GodRayEdgeFalloff, godRayEdgeFalloff);
        material.SetInt(Properties._GodRaySteps, godRaySteps);
    }
    
    void SetCameraProperties(Material material, Camera camera, Light sunlight ) {
        Vector3 godRayScreenPos = camera.WorldToViewportPoint(camera.transform.position - sunlight.transform.forward * 10000.0f);
        material.SetVector(Properties._GodRayScreenPos, godRayScreenPos);
        material.SetVector(Properties._SunDir, sunlight.transform.forward);
        material.SetVector(Properties._SunColor, sunlight.color * sunlight.intensity);
            
        Vector3[] corners = new Vector3[4];
        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, corners);
        material.SetVector(Properties._ViewDirBL, camera.transform.TransformVector(Vector3.Normalize(corners[0])) );
        material.SetVector(Properties._ViewDirTL, camera.transform.TransformVector(Vector3.Normalize(corners[1])) );
        material.SetVector(Properties._ViewDirTR, camera.transform.TransformVector(Vector3.Normalize(corners[2])) );
        material.SetVector(Properties._ViewDirBR, camera.transform.TransformVector(Vector3.Normalize(corners[3])) );
    }
    
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {

        //if (thisCamera == null) {
        //    Graphics.Blit(source, destination);
        //    return;
        //}
        
        if (godRaysAmount == 0f) {
            Graphics.Blit(source, destination);
            return;
        }
        
        Light sunlight = RenderSettings.sun;
        if (sunlight == null) {
            Graphics.Blit(source, destination);
            return;
        }
        
        int screenX = source.width;
        int screenY = source.height;

        Load(godRaysMaterial);

        SetCameraProperties(godRaysMaterial, thisCamera, sunlight);
        
        RenderTexture thresholdTex = RenderTexture.GetTemporary(screenX / 2, screenY / 2, 0, RenderTextureFormat.ARGBHalf);
        RenderTexture zoom1Tex = RenderTexture.GetTemporary(screenX / 4, screenY / 4, 0, RenderTextureFormat.ARGBHalf);
        RenderTexture zoom2Tex = RenderTexture.GetTemporary(screenX / 4, screenY / 4, 0, RenderTextureFormat.ARGBHalf);
        
        // Threshold Scene Color Pass
        Graphics.Blit( source, thresholdTex, godRaysMaterial, 2);

        // Radial Zoom BLur Pass 1
        Graphics.Blit( thresholdTex, zoom1Tex, godRaysMaterial, 3);

        // Radial Zoom BLur Pass 2
        Graphics.Blit( zoom1Tex, zoom2Tex, godRaysMaterial, 4);
        
        // Screen the god rays back to screen
        godRaysMaterial.SetTexture(Properties._GodRayTex, zoom2Tex);

        if (screenValues) {
            // Screen the god rays back to screen
            Graphics.Blit( source, destination, godRaysMaterial, 1);
        } else {
            // Add the god rays back to screen
            Graphics.Blit( source, destination, godRaysMaterial, 0);
        }
        
        RenderTexture.ReleaseTemporary(thresholdTex);
        RenderTexture.ReleaseTemporary(zoom1Tex);
        RenderTexture.ReleaseTemporary(zoom2Tex);
    }
}
