using System.Collections.Generic;
using UnityEngine;

public class RayMarching : MonoBehaviour
{
    public ComputeShader CompShader;
    public Light DirectionalLight;
    public Texture PlanetTexture;
    public Texture PlanetNormalTexture;
    public Texture PlanetaryRingTexture;
    public Texture PlanetaryRingNormalTexture;
    public Texture SkyboxTexture;

    private Camera cam;
    private int Operation = 0;
    private RenderTexture Target;

    private ComputeBuffer MeshBuffer;
    private List<Mesh> MeshList = new List<Mesh>();

    public struct Mesh
    {
        public Vector3 Position;
        public Vector3 Size;
        public int MeshID;
        public int Shape;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        SetupScene();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            IncrementOperation();

    }

    private void OnDisable()
    {
        if (MeshBuffer != null)
            MeshBuffer.Release();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void SetShaderParameters()
    {
        Vector3 forward = DirectionalLight.transform.forward;

        CompShader.SetMatrix("_CameraToWorld", cam.cameraToWorldMatrix);
        CompShader.SetMatrix("_CameraInverseProjection", cam.projectionMatrix.inverse);
        CompShader.SetTexture(0, "_PlanetTexture", PlanetTexture);
        CompShader.SetTexture(0, "_PlanetNormalTexture", PlanetNormalTexture);
        CompShader.SetTexture(0, "_PlanetaryRingTexture", PlanetaryRingTexture);
        CompShader.SetTexture(0, "_PlanetaryRingNormalTexture", PlanetaryRingNormalTexture);
        CompShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        CompShader.SetBuffer(0, "Meshes", MeshBuffer);
        CompShader.SetInt("MeshCount", MeshList.Count);
        CompShader.SetInt("_Operation", Operation);
        CompShader.SetVector("_DirectionalLight", new Vector4(forward.x, forward.y, forward.z, DirectionalLight.intensity));
    }

    private void Render(RenderTexture destination)
    {
        InitializeRenderTexture();

        CompShader.SetTexture(0, "Result", Target);
        int x = Mathf.CeilToInt(Screen.width / 8.0f);
        int y = Mathf.CeilToInt(Screen.height / 8.0f);

        CompShader.Dispatch(0, x, y, 1);
        Graphics.Blit(Target, destination);
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
        }
    }

    private void SetupScene()
    {
        //Red Planet
        MeshList.Add(new Mesh() { Position = new Vector3(0, 0, 3), Size = new Vector3(3, 3, 3), MeshID = 1, Shape = 0 });

        //Orange Planetary Ring
        MeshList.Add(new Mesh() { Position = new Vector3(0, 0, 3), Size = new Vector3(8.0f, 2.5f, 8.0f), MeshID = 2, Shape = 3 });

        //Blue Cube
        //MeshList.Add(new Mesh() { Position = new Vector3(0.0f, 0.0f, 3f), Size = new Vector3(2, 1, 2), Color = new Vector3(0, 0, 1), Shape = 1 });

        //Grey Quad (Floor)
        //MeshList.Add(new Mesh() { Position = new Vector3(0, -0.5f, 3), Size = new Vector3(5, 5, 1), Color = new Vector3(0.5f, 0.5f, 0.5f), Shape = 2 });

        ComputeBuffer buffer = new ComputeBuffer(MeshList.Count, 32);
        buffer.SetData(MeshList);

        MeshBuffer = buffer;
    }

    private void IncrementOperation()
    {
        if (Operation != 3)
            Operation++;
        else
            Operation = 0;
    }
}
