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
    public float SpherePlacementRadius = 100f;
    public int SphereSeed;
    public Light DirectionalLight;
    public Texture SkyboxTexture;
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;

    private Camera camera;
    private ComputeBuffer SphereBuffer;
    private ComputeBuffer MeshBuffer;
    private ComputeBuffer VertexBuffer;
    private ComputeBuffer IndexBuffer;
    private List<Sphere> SphereList = new List<Sphere>();
    private Material AntiAliasingMaterial;
    private RenderTexture Converged;
    private RenderTexture Target;
    private uint CurrentSample = 0;

    private static bool RebuildMeshObjects = false;
    private static List<MeshData> MeshDataList = new List<MeshData>();
    private static List<MeshObject> MeshObjectList = new List<MeshObject>();
    private static List<Vector3> Vertices = new List<Vector3>();
    private static List<int> Indices = new List<int>();

    public struct Sphere
    {
        public float radius;
        public float smoothness;
        public Vector3 albedo;
        public Vector3 emission;
        public Vector3 position;
        public Vector3 specular;
    }

    public struct MeshObject
    {
        public Matrix4x4 LocalToWorldMatrix;
        public int Indices_Offset;
        public int Indices_Count;
        public Vector3 albedo;
        public Vector3 specular;
        public Vector3 emission;
    }

    public class MeshData
    {
        public Mesh mesh;
        public Transform transform;
        public Vector3 albedo;
        public Vector3 emission;
        public Vector3 specular;

        public MeshData(Mesh _mesh, Transform _transform, Vector3 _albedo, Vector3 _emission, Vector3 _specular)
        {
            mesh = _mesh;
            transform = _transform;
            albedo = _albedo;
            emission = _emission;
            specular = _specular;
        }
    }

    private void Awake()
    {
        camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        CurrentSample = 0;
        SetupMeshes();
        SetupSpheres();
    }

    private void OnDisable()
    {
        if (SphereBuffer != null)
            SphereBuffer.Release();
        if (MeshBuffer != null)
            MeshBuffer.Release();
        if (VertexBuffer != null)
            VertexBuffer.Release();
        if (IndexBuffer != null)
            IndexBuffer.Release();
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            CurrentSample = 0;
            transform.hasChanged = false;

            if (SphereList.Count > 0)
            {
                SphereList = SphereList.OrderByDescending(S => Vector3.Distance(S.position, camera.transform.position)).ToList();

                SphereBuffer.Release();

                SphereBuffer = new ComputeBuffer(SphereList.Count, 56);
                SphereBuffer.SetData(SphereList);
            }
        }
        if (DirectionalLight.transform.hasChanged)
        {
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RebuildMeshObjectBuffers();
        SetShaderParameters();
        Render(destination);
    }

    private void SetShaderParameters()
    {
        Vector3 forward = DirectionalLight.transform.forward;
        
        CompShader.SetFloat("_Seed", Random.value);
        CompShader.SetMatrix("_CameraToWorld", camera.cameraToWorldMatrix);
        CompShader.SetMatrix("_CameraInverseProjection", camera.projectionMatrix.inverse);
        CompShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        CompShader.SetVector("_DirectionalLight", new Vector4(forward.x, forward.y, forward.z, DirectionalLight.intensity));
        CompShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        SetComputeBuffer("_Spheres", SphereBuffer);
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

    private void RebuildMeshObjectBuffers()
    {
        if (!RebuildMeshObjects)
            return;

        RebuildMeshObjects = false;
        CurrentSample = 0;

        MeshObjectList.Clear();
        Vertices.Clear();
        Indices.Clear();

        foreach (MeshData data in MeshDataList)
        {
            int FirstVertex = Vertices.Count;
            Vertices.AddRange(data.mesh.vertices);

            int FirstIndex = Indices.Count;
            var indices = data.mesh.GetIndices(0);
            Indices.AddRange(indices.Select(index => index + FirstVertex));

            MeshObjectList.Add(new MeshObject()
            {
                LocalToWorldMatrix = data.transform.localToWorldMatrix,
                Indices_Offset = FirstIndex,
                Indices_Count = indices.Length,
                albedo = data.albedo,
                emission = data.emission,
                specular = data.specular
            });
        }

        CreateComputeBuffer(ref MeshBuffer, MeshObjectList, 108);
        CreateComputeBuffer(ref VertexBuffer, Vertices, 12);
        CreateComputeBuffer(ref IndexBuffer, Indices, 4);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride) where T : struct
    {
        if (buffer != null)
        {
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }

        if (data.Count != 0)
        {
            if (buffer == null)
                buffer = new ComputeBuffer(data.Count, stride);

            buffer.SetData(data);
        }
    }

    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
            CompShader.SetBuffer(0, name, buffer);
    }

    private void SetupSpheres()
    {
        Random.InitState(SphereSeed);

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
            bool Emission = Random.value < 0.15f;

            if (Emission)
            {
                Color emission = Random.ColorHSV(0, 1, 0, 1, 3.0f, 8.0f);
                sphere.emission = new Vector3(emission.r, emission.g, emission.b);
            }
            else
            {      
                bool metal = Random.value < 0.5f;
                sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
                sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;
                sphere.smoothness = Random.value;
            }

            Spheres.Add(sphere);
        }

        SphereBuffer = new ComputeBuffer(Spheres.Count, 56);
        SphereBuffer.SetData(Spheres);

        SphereList = Spheres;
    }

    private void SetupMeshes()
    {
        GameObject CenterCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        CenterCube.transform.localScale = new Vector3(10, 10, 10);
        CenterCube.transform.position = new Vector3(0, 40f, 0);
        Vector3 albedo = new Vector3(0.8f, 0.8f, 0.8f);
        Vector3 emission = new Vector3(0f, 0f, 0f);
        Vector3 specular = new Vector3(1f, 1f, 1f);
        MeshDataList.Add(new MeshData(CenterCube.GetComponent<MeshFilter>().sharedMesh, CenterCube.transform, albedo, emission, specular));

        GameObject RedCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        RedCube.transform.localScale = new Vector3(20, 2, 20);
        RedCube.transform.position = new Vector3(0f, 34f, 0f);
        albedo = new Vector3(0, 0, 0);
        emission = new Vector3(1f, 0, 0);
        specular = new Vector3(1, 1, 1);
        MeshDataList.Add(new MeshData(RedCube.GetComponent<MeshFilter>().sharedMesh, RedCube.transform, albedo, emission, specular));

        //GameObject BlueSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //BlueSphere.transform.localScale = new Vector3(8, 8, 8);
        //BlueSphere.transform.position = new Vector3(0, 49f, 0);
        //albedo = new Vector3(0, 0, 0);
        //emission = new Vector3(0, 0, 1);
        //specular = new Vector3(1, 1, 1);
        //MeshDataList.Add(new MeshData(BlueSphere.GetComponent<MeshFilter>().sharedMesh, BlueSphere.transform, albedo, emission, specular));

        
        RebuildMeshObjects = true;
    }
}
