using UnityEngine;

[DisallowMultipleComponent]
public sealed class GoldenCloudVortexDemo : MonoBehaviour
{
    public Camera viewCamera;
    public MeshRenderer clouds;
    public Vector2[] flashSites;
    [Range(0,1.5f)] public float flashStrength = 1;
    public const float LoopSeconds = 12.8f;
    const float PassageLoopSeconds = 19.2f;
    const float PassageSpeed = 32f * 3f / PassageLoopSeconds;
    public const string CameraSettingsResource = "GoldenCloudCamera";

    [System.Serializable]
    public sealed class CameraSettings
    {
        public bool fixedCamera = true;
    }

    public static CameraSettings ReadCameraSettings()
    {
        var asset=Resources.Load<TextAsset>(CameraSettingsResource);
        var settings=new CameraSettings();
        if(asset!=null) JsonUtility.FromJsonOverwrite(asset.text,settings);
        return settings;
    }

    bool? fixedCamera;
    public bool FixedCamera
    {
        get
        {
            if(!fixedCamera.HasValue) fixedCamera=ReadCameraSettings().fixedCamera;
            return fixedCamera.Value;
        }
        set { fixedCamera=value; }
    }

    MaterialPropertyBlock properties;
    int sessionSeed;
    float elapsed;
    float previousAspect;
    bool traveling;
    float travelStartTime;
    Vector3 cameraStart,cloudStart,travelDirection;

    void UpdateCameraTravel(float seconds)
    {
        if(FixedCamera)
        {
            RestoreView();
            return;
        }
        if(!traveling)
        {
            cameraStart=viewCamera.transform.position;
            cloudStart=clouds.transform.position;
            travelDirection=viewCamera.transform.forward;
            travelStartTime=seconds;
            traveling=true;
        }
        Vector3 offset=travelDirection*((seconds-travelStartTime)*PassageSpeed);
        SetRigPosition(offset);
    }

    // The distant opening travels with the camera. The material provides
    // the depth-dependent motion of the nearer cloud banks.
    void SetRigPosition(Vector3 offset)
    {
        var cameraTransform=viewCamera.transform;
        var cloudTransform=clouds.transform;
        if(cameraTransform==cloudTransform || !cameraTransform.IsChildOf(cloudTransform))
            cameraTransform.position=cameraStart+offset;
        if(!cloudTransform.IsChildOf(cameraTransform)) cloudTransform.position=cloudStart+offset;
    }

    void RestoreView()
    {
        if(!traveling) return;
        if(viewCamera!=null && clouds!=null) SetRigPosition(Vector3.zero);
        traveling=false;
    }

    void OnDisable() => RestoreView();

    void Start()
    {
        sessionSeed = Random.Range(0,65536);
        SetTime(0,sessionSeed);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        SetTime(elapsed,sessionSeed);
    }

    static float Random01(int seed)
    {
        uint h=unchecked((uint)seed*747796405u+2891336453u);
        h=unchecked(((h>>(int)((h>>28)+4))^h)*277803737u);
        return ((h^(h>>22))&65535u)/65535f;
    }

    static float Ramp(float a,float b,float x) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));

    // Explicit time and seed also allow frame-exact comparison of both renderers.
    public void SetTime(float seconds,int seed)
    {
        if(properties==null) properties=new MaterialPropertyBlock();
        UpdateCameraTravel(seconds);
        float aspect=viewCamera.aspect;
        if(!Mathf.Approximately(previousAspect,aspect))
        {
            float scale=Mathf.Max(1,aspect/(16f/9f));
            viewCamera.fieldOfView=2*Mathf.Rad2Deg*Mathf.Atan(Mathf.Tan(25*Mathf.Deg2Rad)/scale);
            previousAspect=aspect;
        }
        int index=Mathf.FloorToInt(seconds/3.2f);
        int key=seed+index*7;
        int slot=Mathf.Min(flashSites.Length-1,(int)(Random01(key+3)*flashSites.Length));
        Vector2 site=flashSites[slot];
        float age=seconds-index*3.2f-.48f-.29f*Random01(key+1);
        float heat=Ramp(0,.32f,age)*(1-Ramp(.32f,1.26f,age));
        float radius=Mathf.Lerp(.35f,.44f,Random01(key+2));
        float duration=FixedCamera?LoopSeconds:PassageLoopSeconds;
        properties.SetFloat("_Cycle",Mathf.Repeat(seconds/duration,1));
        properties.SetFloat("_PassageMode",FixedCamera?0f:1f);
        properties.SetVector("_Storm",new Vector4(site.x,site.y,radius,heat*flashStrength));
        clouds.SetPropertyBlock(properties);
    }
}
