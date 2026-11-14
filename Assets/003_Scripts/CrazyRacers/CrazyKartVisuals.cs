using UnityEngine;

public static class CrazyKartVisuals
{
    public static GameObject CreateKartModel(Color color)
    {
        var root = new GameObject("Modelo Kart Visual");
        
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        body.transform.localScale = new Vector3(1.5f, 0.55f, 2.4f);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Metallic", 0.8f);
        mat.SetFloat("_Smoothness", 0.7f);
        body.GetComponent<Renderer>().sharedMaterial = mat;
        Object.Destroy(body.GetComponent<Collider>());

        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.transform.SetParent(root.transform, false);
        nose.transform.localPosition = new Vector3(0f, 0.25f, 1.2f);
        nose.transform.localScale = new Vector3(1.2f, 0.4f, 1.1f);
        var noseMat = new Material(mat);
        noseMat.color = color * 0.75f;
        nose.GetComponent<Renderer>().sharedMaterial = noseMat;
        Object.Destroy(nose.GetComponent<Collider>());

        float[] wheelX = { -0.9f, 0.9f, -0.9f, 0.9f };
        float[] wheelZ = { -1.2f, -1.2f, 1.1f, 1.1f };
        for (int i = 0; i < 4; i++)
        {
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Rueda " + i;
            wheel.transform.SetParent(root.transform, false);
            wheel.transform.localPosition = new Vector3(wheelX[i], 0.18f, wheelZ[i]);
            wheel.transform.localScale = new Vector3(0.55f, 0.3f, 0.55f);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var wheelMat = new Material(mat);
            wheelMat.color = new Color(0.1f, 0.1f, 0.1f);
            wheelMat.SetFloat("_Metallic", 0.2f);
            wheel.GetComponent<Renderer>().sharedMaterial = wheelMat;
            Object.Destroy(wheel.GetComponent<Collider>());
        }

        var driver = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        driver.name = "Piloto";
        driver.transform.SetParent(root.transform, false);
        driver.transform.localPosition = new Vector3(0f, 0.85f, -0.2f);
        driver.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        var skinMat = new Material(mat);
        skinMat.color = new Color(0.95f, 0.78f, 0.6f);
        driver.GetComponent<Renderer>().sharedMaterial = skinMat;
        Object.Destroy(driver.GetComponent<Collider>());

        var helmet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        helmet.name = "Casco";
        helmet.transform.SetParent(driver.transform, false);
        helmet.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        helmet.transform.localScale = Vector3.one * 0.92f;
        helmet.GetComponent<Renderer>().sharedMaterial = mat;
        Object.Destroy(helmet.GetComponent<Collider>());

        return root;
    }
}
