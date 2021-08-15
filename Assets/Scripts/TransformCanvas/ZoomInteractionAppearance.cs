using UnityEngine;
using UnityEngine.UI;

public class ZoomInteractionAppearance : MonoBehaviour
{
    public int N = 5;
    public Transform cameraTransform;

    private LineRenderer lineRenderer;
    private RectTransform rectTransform;
    private Text label;

    void Start()
    {
        rectTransform = GetComponentInChildren<RectTransform>();
        lineRenderer = GetComponent<LineRenderer>();
        label = GetComponentInChildren<Text>();
        lineRenderer.positionCount = N + 1;
        lineRenderer.enabled = false;
    }

    
    public void OnZoomStart(Vector3 primaryHand, Vector3 secondaryHand)
    {
        lineRenderer.enabled = true;
    }

    public void OnZoomUpdate(Vector3 primaryHand, Vector3 secondaryHand, bool zoomSuccess, float newScale)
    {

        Vector3[] positions = new Vector3[N + 1];
        float step = 1f / N;

        for (int i = 0; i <= N; i++)
        {
            float t = i * step;
            positions[i] = Vector3.Lerp(primaryHand, secondaryHand, t);
        }

        lineRenderer.SetPositions(positions);

        // Text
        label.enabled = true;
        label.text = "x " + newScale.ToString("F1");
        rectTransform.anchoredPosition3D = Vector3.Lerp(primaryHand, secondaryHand, 0.5f);
        rectTransform.LookAt(cameraTransform.position);

        // Color
        if (!zoomSuccess)
            lineRenderer.material.color = Color.red;
        else
            lineRenderer.material.color = Color.yellow;
    }

    public void OnZoomEnd()
    {
        label.enabled = false;
        lineRenderer.enabled = false;
    }
}
