using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using static UnityEngine.GraphicsBuffer;

public class RayMarching : MonoBehaviour
{
    public ComputeShader CompShader;
    public Light DirectionalLight;
    public Texture SkyboxTexture;

    private Camera cam;
    private ComputeBuffer MeshBuffer;
    private ComputeBuffer VertexBuffer;
    private ComputeBuffer IndexBuffer;
    private Material AntiAliasingMaterial;
    private RenderTexture Converged;
    private RenderTexture Target;
    private uint CurrentSample = 0;

    private static List<MeshObject> MeshObjectList = new List<MeshObject>();
    private static List<Vector3> Vertices = new List<Vector3>();
    private static List<int> Indices = new List<int>();

    public struct MeshObject
    {
        public Matrix4x4 LocalToWorldMatrix;
        public int Indices_Offset;
        public int Indices_Count;
        public Vector3 albedo;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        CurrentSample = 0;
        //SetupMeshes();
    }

    private void OnDisable()
    {
        if (MeshBuffer != null)
            MeshBuffer.Release();
        if (VertexBuffer != null)
            VertexBuffer.Release();
        if (IndexBuffer != null)
            IndexBuffer.Release();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void SetShaderParameters()
    {
        Vector3 forward = DirectionalLight.transform.forward;

        CompShader.SetFloat("_Seed", Random.value);
        CompShader.SetMatrix("_CameraToWorld", cam.cameraToWorldMatrix);
        CompShader.SetMatrix("_CameraInverseProjection", cam.projectionMatrix.inverse);
        CompShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        CompShader.SetVector("_DirectionalLight", new Vector4(forward.x, forward.y, forward.z, DirectionalLight.intensity));
        CompShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        SetComputeBuffer("_MeshObjects", MeshBuffer);
        SetComputeBuffer("_Vertices", VertexBuffer);
        SetComputeBuffer("_Indices", IndexBuffer);
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

        Graphics.Blit(Target, Converged, AntiAliasingMaterial);
        Graphics.Blit(Converged, destination);

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

        if (Converged == null || Converged.width != Screen.width || Converged.height != Screen.height)
        {
            if (Converged != null)
                Converged.Release();

            Converged = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Converged.enableRandomWrite = true;
            Converged.Create();
        }
    }

    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
            CompShader.SetBuffer(0, name, buffer);
    }
}
