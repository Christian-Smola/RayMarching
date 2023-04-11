using UnityEngine;

public class RayMarching : MonoBehaviour
{
    public ComputeShader CompShader;
    public Texture SkyboxTexture;

    private Camera cam;
    private RenderTexture Target;

    private void Awake()
    {
        cam = GetComponent<Camera>();
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
}
