using UnityEngine;

/// <summary>
/// Procedural 3D Character Model Builder.
/// Generates a full 3D humanoid cartoon character (Fall Guys style)
/// with head, visor, torso, arms, hands, and legs, wired with Character3DAnimator.
/// </summary>
public static class CharacterModelBuilder
{
    public static GameObject BuildRunnerCharacter(string name, Color bodyColor, bool isPlayer = false)
    {
        // Root Container
        GameObject root = new GameObject(name);
        root.layer = LayerMask.NameToLayer("Default");

        // Rigidbody
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 1.2f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 1.0f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Capsule Collider (Clean, smooth collisions across hex tiles)
        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.85f, 0);
        col.radius = 0.42f;
        col.height = 1.7f;
        col.direction = 1; // Y-axis

        // Materials
        Material bodyMat = new Material(Shader.Find("Standard"));
        bodyMat.color = bodyColor;

        Material faceMat = new Material(Shader.Find("Standard"));
        faceMat.color = new Color(0.96f, 0.96f, 0.96f); // Clean white visor

        Material darkMat = new Material(Shader.Find("Standard"));
        darkMat.color = new Color(0.1f, 0.1f, 0.12f); // Black eyes / shoes

        // 1. Model Root
        GameObject modelRoot = new GameObject("VisualModel");
        modelRoot.transform.SetParent(root.transform, false);

        // 2. Torso / Body (Chubby bean body)
        GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Torso";
        Object.DestroyImmediate(torso.GetComponent<Collider>());
        torso.transform.SetParent(modelRoot.transform, false);
        torso.transform.localPosition = new Vector3(0, 0.92f, 0);
        torso.transform.localScale = new Vector3(0.72f, 0.65f, 0.65f);
        torso.GetComponent<MeshRenderer>().material = bodyMat;

        // 3. Head (Rounded head atop torso)
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        Object.DestroyImmediate(head.GetComponent<Collider>());
        head.transform.SetParent(torso.transform, false);
        head.transform.localPosition = new Vector3(0, 0.62f, 0.05f);
        head.transform.localScale = new Vector3(0.95f, 0.85f, 0.95f);
        head.GetComponent<MeshRenderer>().material = bodyMat;

        // 4. Visor Faceplate
        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visor.name = "VisorFace";
        Object.DestroyImmediate(visor.GetComponent<Collider>());
        visor.transform.SetParent(head.transform, false);
        visor.transform.localPosition = new Vector3(0, 0.02f, 0.38f);
        visor.transform.localScale = new Vector3(0.68f, 0.52f, 0.38f);
        visor.GetComponent<MeshRenderer>().material = faceMat;

        // Left Eye
        GameObject eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeL.name = "EyeL";
        Object.DestroyImmediate(eyeL.GetComponent<Collider>());
        eyeL.transform.SetParent(visor.transform, false);
        eyeL.transform.localPosition = new Vector3(-0.24f, 0.06f, 0.32f);
        eyeL.transform.localScale = new Vector3(0.18f, 0.28f, 0.18f);
        eyeL.GetComponent<MeshRenderer>().material = darkMat;

        // Right Eye
        GameObject eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeR.name = "EyeR";
        Object.DestroyImmediate(eyeR.GetComponent<Collider>());
        eyeR.transform.SetParent(visor.transform, false);
        eyeR.transform.localPosition = new Vector3(0.24f, 0.06f, 0.32f);
        eyeR.transform.localScale = new Vector3(0.18f, 0.28f, 0.18f);
        eyeR.GetComponent<MeshRenderer>().material = darkMat;

        // 5. Left Arm Pivot & Limb
        GameObject leftArmPivot = new GameObject("LeftArmPivot");
        leftArmPivot.transform.SetParent(torso.transform, false);
        leftArmPivot.transform.localPosition = new Vector3(-0.48f, 0.25f, 0);

        GameObject leftArmMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.DestroyImmediate(leftArmMesh.GetComponent<Collider>());
        leftArmMesh.name = "ArmMesh";
        leftArmMesh.transform.SetParent(leftArmPivot.transform, false);
        leftArmMesh.transform.localPosition = new Vector3(0, -0.28f, 0);
        leftArmMesh.transform.localScale = new Vector3(0.25f, 0.32f, 0.25f);
        leftArmMesh.GetComponent<MeshRenderer>().material = bodyMat;

        // Left Hand
        GameObject leftHand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(leftHand.GetComponent<Collider>());
        leftHand.name = "Hand";
        leftHand.transform.SetParent(leftArmPivot.transform, false);
        leftHand.transform.localPosition = new Vector3(0, -0.58f, 0);
        leftHand.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
        leftHand.GetComponent<MeshRenderer>().material = faceMat;

        // 6. Right Arm Pivot & Limb
        GameObject rightArmPivot = new GameObject("RightArmPivot");
        rightArmPivot.transform.SetParent(torso.transform, false);
        rightArmPivot.transform.localPosition = new Vector3(0.48f, 0.25f, 0);

        GameObject rightArmMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.DestroyImmediate(rightArmMesh.GetComponent<Collider>());
        rightArmMesh.name = "ArmMesh";
        rightArmMesh.transform.SetParent(rightArmPivot.transform, false);
        rightArmMesh.transform.localPosition = new Vector3(0, -0.28f, 0);
        rightArmMesh.transform.localScale = new Vector3(0.25f, 0.32f, 0.25f);
        rightArmMesh.GetComponent<MeshRenderer>().material = bodyMat;

        // Right Hand
        GameObject rightHand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(rightHand.GetComponent<Collider>());
        rightHand.name = "Hand";
        rightHand.transform.SetParent(rightArmPivot.transform, false);
        rightHand.transform.localPosition = new Vector3(0, -0.58f, 0);
        rightHand.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
        rightHand.GetComponent<MeshRenderer>().material = faceMat;

        // 7. Left Leg Pivot & Foot
        GameObject leftLegPivot = new GameObject("LeftLegPivot");
        leftLegPivot.transform.SetParent(modelRoot.transform, false);
        leftLegPivot.transform.localPosition = new Vector3(-0.22f, 0.55f, 0);

        GameObject leftLegMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.DestroyImmediate(leftLegMesh.GetComponent<Collider>());
        leftLegMesh.name = "LegMesh";
        leftLegMesh.transform.SetParent(leftLegPivot.transform, false);
        leftLegMesh.transform.localPosition = new Vector3(0, -0.22f, 0);
        leftLegMesh.transform.localScale = new Vector3(0.24f, 0.26f, 0.24f);
        leftLegMesh.GetComponent<MeshRenderer>().material = bodyMat;

        GameObject leftFoot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(leftFoot.GetComponent<Collider>());
        leftFoot.name = "Foot";
        leftFoot.transform.SetParent(leftLegPivot.transform, false);
        leftFoot.transform.localPosition = new Vector3(0, -0.48f, 0.08f);
        leftFoot.transform.localScale = new Vector3(0.30f, 0.18f, 0.38f);
        leftFoot.GetComponent<MeshRenderer>().material = darkMat;

        // 8. Right Leg Pivot & Foot
        GameObject rightLegPivot = new GameObject("RightLegPivot");
        rightLegPivot.transform.SetParent(modelRoot.transform, false);
        rightLegPivot.transform.localPosition = new Vector3(0.22f, 0.55f, 0);

        GameObject rightLegMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.DestroyImmediate(rightLegMesh.GetComponent<Collider>());
        rightLegMesh.name = "LegMesh";
        rightLegMesh.transform.SetParent(rightLegPivot.transform, false);
        rightLegMesh.transform.localPosition = new Vector3(0, -0.22f, 0);
        rightLegMesh.transform.localScale = new Vector3(0.24f, 0.26f, 0.24f);
        rightLegMesh.GetComponent<MeshRenderer>().material = bodyMat;

        GameObject rightFoot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(rightFoot.GetComponent<Collider>());
        rightFoot.name = "Foot";
        rightFoot.transform.SetParent(rightLegPivot.transform, false);
        rightFoot.transform.localPosition = new Vector3(0, -0.48f, 0.08f);
        rightFoot.transform.localScale = new Vector3(0.30f, 0.18f, 0.38f);
        rightFoot.GetComponent<MeshRenderer>().material = darkMat;

        // 9. Wire Character3DAnimator
        Character3DAnimator anim = root.AddComponent<Character3DAnimator>();

        // Link references via reflection / properties
        var type = typeof(Character3DAnimator);
        type.GetField("torso", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(anim, torso.transform);
        type.GetField("head", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(anim, head.transform);
        type.GetField("leftArm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(anim, leftArmPivot.transform);
        type.GetField("rightArm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(anim, rightArmPivot.transform);
        type.GetField("leftLeg", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(anim, leftLegPivot.transform);
        type.GetField("rightLeg", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(anim, rightLegPivot.transform);
        type.GetField("rb", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(anim, rb);

        root.AddComponent<CharacterSquashAndStretch>();

        return root;
    }
}
