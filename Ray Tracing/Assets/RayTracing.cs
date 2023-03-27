//Based on the following article by David Kuri
//http://blog.three-eyed-games.com/2018/05/03/gpu-ray-tracing-in-unity-part-1/ 

using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static RayTracing;

public class RayTracing : MonoBehaviour
{
    public ComputeShader CompShader;
    public Light DirectionalLight;
    public Texture SkyboxTexture;
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100f;

    private Camera camera;
    private ComputeBuffer SphereBuffer;
    private List<Sphere> SphereList = new List<Sphere>();
    private Material AntiAliasingMaterial;
    private RenderTexture Target;
    private uint CurrentSample = 0;

    public struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
    }

    private void Awake()
    {
        camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        CurrentSample = 0;
        SetupScene();
    }

    private void OnDisable()
    {
        if (SphereBuffer != null)
            SphereBuffer.Release();
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            CurrentSample = 0;
            transform.hasChanged = false;

            SphereList = SphereList.OrderByDescending(S => Vector3.Distance(S.position, camera.transform.position)).ToList();

            SphereBuffer.Release();

            SphereBuffer = new ComputeBuffer(SphereList.Count, 40);
            SphereBuffer.SetData(SphereList);
        }
        if (DirectionalLight.transform.hasChanged)
        {
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void SetShaderParameters()
    {
        Vector3 forward = DirectionalLight.transform.forward;
        CompShader.SetVector("_DirectionalLight", new Vector4(forward.x, forward.y, forward.z, DirectionalLight.intensity));
        CompShader.SetBuffer(0, "_Spheres", SphereBuffer);
        CompShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        CompShader.SetMatrix("_CameraToWorld", camera.cameraToWorldMatrix);
        CompShader.SetMatrix("_CameraInverseProjection", camera.projectionMatrix.inverse);
        CompShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
    }

    private void Render(RenderTexture destination)
    {
        InitializeRenderTexture();

        CompShader.SetTexture(0, "Result", Target);
        int x = Mathf.CeilToInt(Screen.width / 8.0f);
        int y = Mathf.CeilToInt(Screen.height / 8.0f);

        if (AntiAliasingMaterial == null)
            AntiAliasingMaterial = new Material(Shader.Find("Hidden/AntiAliasing"));

        AntiAliasingMaterial.SetFloat("_Sample", CurrentSample);

        CompShader.Dispatch(0, x, y, 1);

        Graphics.Blit(Target, destination, AntiAliasingMaterial);

        CurrentSample++;
    }

    private void InitializeRenderTexture()
    {
        if (Target == null || Target.width != Screen.width || Target.height != Screen.height)
        {
            if (Target != null)
                Target.Release();

            Target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Target.enableRandomWrite = true;
            Target.Create();

            CurrentSample = 0;
        }
    }

    private void SetupScene()
    {
        List<Sphere> Spheres = new List<Sphere>();

        for (int x = 0; x < SpheresMax; x++)
        {
            Sphere sphere = new Sphere();

            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 RandomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(RandomPos.x, sphere.radius, RandomPos.y);

            bool Rejected = false;

            foreach (Sphere other in Spheres)
            {
                float MinDistance = sphere.radius + other.radius;

                if (Vector3.SqrMagnitude(sphere.position - other.position) < MinDistance * MinDistance)
                {
                    Rejected = true;
                    break;
                }
            }

            if (Rejected)
                continue;

            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            Spheres.Add(sphere);
        }

        SphereBuffer = new ComputeBuffer(Spheres.Count, 40);
        SphereBuffer.SetData(Spheres);

        SphereList = Spheres;
    }
}
