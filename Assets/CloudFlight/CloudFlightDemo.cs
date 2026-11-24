using UnityEngine;

public sealed class CloudFlightDemo : MonoBehaviour
{
    public Camera flightCamera;
    public Transform aircraft;
    public Transform[] sections;
    public Renderer[] cloudRenderers;
    [Range(0, 1.5f)] public float flashStrength = 1;
    public bool wideView;
    public TextAsset flashSites;
    [System.Serializable] class SiteList { public Vector3[] points; }
    Vector3[] sites;
    int flashSeed;
    MaterialPropertyBlock properties;
    float elapsed;

    void Start() { flashSeed = Random.Range(0,65536); Pose(0); }

    void Update()
    {
        bool tap = Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began;
        if (tap || Input.GetKeyDown(KeyCode.Space)) wideView = !wideView;
        elapsed += Time.deltaTime;
        Pose(elapsed);
    }

    static Vector3 Route(float z)
    {
        float a = z * (2 * Mathf.PI / 192);
        return new Vector3(11*Mathf.Sin(a+.25f)+5*Mathf.Sin(2*a-.6f),
            1.6f+3.4f*Mathf.Sin(a-.6f)+1.4f*Mathf.Sin(2*a+.9f),z);
    }

    static float Random01(int seed)
    {
        uint x = unchecked((uint)seed * 747796405u + 2891336453u);
        x = unchecked(((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u);
        return ((x ^ (x >> 22)) & 65535u) / 65535f;
    }

    static float Ramp(float a,float b,float t) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));

    public void Pose(float seconds)
    {
        if (properties == null) properties = new MaterialPropertyBlock();
        float z = Mathf.Repeat(seconds,24)*8;
        Vector3 plane = Route(z);
        Vector3 f = (Route(z+1)-Route(z-1)).normalized;
        aircraft.SetPositionAndRotation(plane,Quaternion.LookRotation(f)*Quaternion.Euler(0,0,13*Mathf.Sin(z*.056f-.5f)));
        Vector3 eye = Route(z-(wideView?22:16)) + Vector3.up*(wideView?5.5f:3.3f);
        float bay=Mathf.Pow(.5f+.5f*Mathf.Cos(z*Mathf.PI/96-.26f),6);
        eye+=new Vector3(4.8f*bay,3.4f*bay,0);
        Vector3 aim = Route(z+12)+Vector3.up*1.6f;
        flightCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(aim-eye));

        int index=Mathf.FloorToInt(seconds/3.2f);
        float eventSeconds=index*3.2f;
        int seed=flashSeed+7*index;
        float age=seconds-eventSeconds-.48f-.29f*Random01(seed+1);
        float heat=Ramp(0,.32f,age)*(1-Ramp(.32f,1.26f,age));
        if(sites==null) sites=JsonUtility.FromJson<SiteList>(flashSites.text).points;
        float flashZ=eventSeconds*8+11+7*Random01(seed+2)-192*Mathf.Floor(seconds/24);
        int slice=Mathf.FloorToInt(flashZ/6+.5f);
        int wrapped=(slice%32+32)%32;
        int site=Mathf.Min(7,Mathf.FloorToInt(8*Random01(seed+3)));
        Vector3 spot=sites[8*wrapped+site];
        spot.z+=6*(slice-wrapped);
        properties.SetVector("_Pulse",new Vector4(spot.x,spot.y,spot.z,heat*flashStrength));
        for(int i=0;i<sections.Length;i++)
        {
            float shift=192*Mathf.Ceil((eye.z-3-(i+1)*24)/192);
            float start=i*24+shift;
            bool show=start<eye.z+96;
            Transform section=sections[i];
            if(section.gameObject.activeSelf!=show) section.gameObject.SetActive(show);
            if(!show) continue;
            section.localPosition=Vector3.forward*shift;
            cloudRenderers[2*i].SetPropertyBlock(properties);
            var mist=cloudRenderers[2*i+1];
            mist.SetPropertyBlock(properties);
            mist.sortingOrder=-Mathf.RoundToInt((start-eye.z)/24);
        }
    }
}
