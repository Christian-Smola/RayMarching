using System.Collections.Generic;
using UnityEngine;

public class RayMarching : MonoBehaviour
{
    public ComputeShader CompShader;
    public Texture SkyboxTexture;

    private Camera cam;
    private RenderTexture Target;

    private ComputeBuffer MeshBuffer;
    private List<Mesh> MeshList = new List<Mesh>();

    public struct Mesh
    {
        public Vector3 Position;
        public Vector3 Size;
        public Vector3 Color;
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

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void SetShaderParameters()
    {
        CompShader.SetMatrix("_CameraToWorld", cam.cameraToWorldMatrix);
        CompShader.SetMatrix("_CameraInverseProjection", cam.projectionMatrix.inverse);
        CompShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        CompShader.SetBuffer(0, "Meshes", MeshBuffer);
        CompShader.SetInt("MeshCount", MeshList.Count);
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
        //Red Sphere
        MeshList.Add(new Mesh() { Position = new Vector3(2, -0.5f, 3), Size = new Vector3(1, 1, 1), Color = new Vector3(1, 0, 0), Shape = 0 });

        //Blue Cube
        MeshList.Add(new Mesh() { Position = new Vector3(-2, -0.5f, 3), Size = new Vector3(1, 1, 1), Color = new Vector3(0, 0, 1), Shape = 1 });

        ComputeBuffer buffer = new ComputeBuffer(MeshList.Count, 40);
        buffer.SetData(MeshList);

        MeshBuffer = buffer;
    }
}
