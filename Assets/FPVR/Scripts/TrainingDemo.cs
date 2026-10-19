using UnityEngine;

namespace FPVR
{
    public class TrainingDemo : MonoBehaviour
    {
        [Header("Saved scene references")]
        public DroneFlight drone;
        public DroneView pilot;
        [Tooltip("Direct child gates are flown in Hierarchy order, top to bottom.")]
        public Transform gateContainer;
        public Transform[] rotors;
        public TextMesh vrHud;
        [Header("Course appearance")]
        public Color activeGateColor = new Color(1, .45f, .1f);
        public Color passedGateColor = new Color(.25f, .95f, .5f);
        public float droneClearanceRadius = .29f;
        TrainingGate[] gates;
        int nextGate; float elapsed; bool finished; Vector3 previous;
        GUIStyle heading, body;
        void Start()
        {
            if (!drone || !pilot || !gateContainer)
            { Debug.LogError("Training Manager: assign Drone, Pilot and Gate Container.", this); enabled = false; return; }
            var list = new System.Collections.Generic.List<TrainingGate>();
            foreach (Transform child in gateContainer)
            {
                var gate = child.GetComponent<TrainingGate>();
                if (gate && gate.isActiveAndEnabled) list.Add(gate);
            }
            gates = list.ToArray();
            // Ensure editable TextMesh labels use a supported built-in font and its material.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var label in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                if (label.gameObject.scene != gameObject.scene) continue;
                label.font = font;
                label.GetComponent<Renderer>().sharedMaterial = font.material;
            }
            Time.fixedDeltaTime = 1f / 90f;
            Application.targetFrameRate = 90;
            drone.ResetCourse += ResetRun;
            ResetRun();
        }
        void OnDestroy() { if (drone) drone.ResetCourse -= ResetRun; }
        void ResetRun()
        {
            nextGate = 0; elapsed = 0; finished = false; previous = drone.transform.position;
            Highlight(); if (pilot.IsVR) pilot.Recenter();
        }
        void Highlight()
        {
            for (int i = 0; i < gates.Length; i++)
                gates[i].Highlight(i < nextGate ? 2 : (i == nextGate ? 1 : 0), activeGateColor, passedGateColor);
        }
        void Update()
        {
            if (gates == null) return;
            if (drone.armed && !finished) elapsed += Time.deltaTime;
            Vector3 now = drone.transform.position;
            if (drone.armed && !finished && nextGate < gates.Length && gates[nextGate].Crossed(previous, now, droneClearanceRadius))
            {
                nextGate++; finished = nextGate == gates.Length; Highlight();
            }
            previous = now;
            if (drone.armed && rotors != null)
                for (int i = 0; i < rotors.Length; i++)
                    if (rotors[i]) rotors[i].Rotate(0, (i % 2 == 0 ? 1 : -1) * 1800 * Time.deltaTime, 0);
            if (vrHud)
            {
                vrHud.gameObject.SetActive(pilot.IsVR);
                if (pilot.IsVR) vrHud.text = Status() + "\nGate " + nextGate + "/" + gates.Length + "   " + elapsed.ToString("F1") + " s\nMove a stick to fly   X: reset";
            }
        }
        string Status() => drone.crashed ? "CRASHED / RESET TO RETRY" : finished ? "COURSE COMPLETE" : drone.armed ? "FLYING / ASSISTED" : "READY / PRESS ANY FLIGHT KEY";
        void OnGUI()
        {
            if(drone==null || pilot==null || gates==null || pilot.IsVR) return;
            if(heading==null)
            {
                heading=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold}; heading.normal.textColor=new Color(.2f,.95f,1);
                body=new GUIStyle(GUI.skin.label){fontSize=16}; body.normal.textColor=Color.white;
            }
            GUI.Box(new Rect(16,16,420,215),GUIContent.none);
            GUI.Label(new Rect(32,28,390,36),"FPVR  /  FLIGHT LAB",heading);
            GUI.Label(new Rect(32,69,390,28),Status(),body);
            GUI.Label(new Rect(32,101,390,28),"GATES  "+nextGate+" / "+gates.Length+"        TIME  "+elapsed.ToString("F1")+" s",body);
            GUI.Label(new Rect(32,133,390,28),"ALT  "+Mathf.Max(0,drone.transform.position.y).ToString("F1")+" m        SPEED  "+drone.Body.linearVelocity.magnitude.ToString("F1")+" m/s",body);
            if(GUI.Button(new Rect(32,175,175,36),drone.armed?"Disarm":"Arm / start")) { if(!drone.crashed) drone.armed=!drone.armed; }
            if(GUI.Button(new Rect(220,175,195,36),"Reset run")) drone.ResetDrone();
            GUI.Box(new Rect(16,Screen.height-98,790,82),GUIContent.none);
            GUI.Label(new Rect(30,Screen.height-88,770,30),"W / S: forward / back    A / D: turn    Q / E: strafe",body);
            GUI.Label(new Rect(30,Screen.height-58,770,30),"Space: up    Shift: down    R: reset    C: FPV / chase cam",body);
            if(drone.crashed || finished)
                GUI.Label(new Rect(Screen.width/2-200,Screen.height/2-30,500,60),drone.crashed?"CRASHED — Press R to retry":"COURSE COMPLETE — "+elapsed.ToString("F1")+" s",heading);
        }
    }
}
