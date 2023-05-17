using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TouchPhase = UnityEngine.TouchPhase;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Text;
using System.IO;
using UnityEngine.SceneManagement;

public class ARSessionOriginScript : MonoBehaviour
{

    // gameobjects of all the different menus
    [SerializeField]
    public GameObject gen_menu; // general
    [SerializeField]
    public GameObject placement_menu; // during placement
    [SerializeField]
    public GameObject manip_menu; // during manipulation
    [SerializeField]
    public GameObject main_menu; // for save reset

    // specific objects menu items references
    [SerializeField]
    public GameObject ball_button;
    [SerializeField]
    public GameObject hole_button;
    [SerializeField]
    public GameObject paper_button;
    [SerializeField]
    public GameObject rotate_slider;
    [SerializeField]
    public GameObject save_button;
    [SerializeField]
    public GameObject warning_text;
    [SerializeField]
    public GameObject saved_text;

    // object prefabs
    [SerializeField]
    public GameObject bush_prefab;
    [SerializeField]
    public GameObject crate_prefab;
    [SerializeField]
    public GameObject golfball_prefab;
    [SerializeField]
    public GameObject golfhole_prefab;
    [SerializeField]
    public GameObject paper_prefab;
    [SerializeField]
    public GameObject slabs_prefab;
    [SerializeField]
    public GameObject rock_prefab;

    // material indicating selection
    [SerializeField]
    public Material highlight;

    // state
    string state;

    // selected object, original position and rotation
    GameObject selected;
    Vector3 selected_position;
    Quaternion selected_rotation;

    // selected object original material
    Material selected_mat;

    // list of objects in the environment
    static List<GameObject> objects_list;

    // for dragging elements
    private bool hold;
    private Vector2 touch_pos;

    // for player input
    PlayerInput playerInput;

    // raycast manager
    private ARRaycastManager raycastManager;

    // hits list
    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // max distance for ray
    public static float MAX_DIST = 21.0f;

    // list of actions
    ActionsMap action_map;

    // Start is called before the first frame update
    void Start()
    {
        // get player input component
        playerInput = GetComponent<PlayerInput>();

        // get raycast manager
        raycastManager = GetComponent<ARRaycastManager>();

        // only have the general layout when begin
        gen_menu.SetActive(true);
        placement_menu.SetActive(false);
        manip_menu.SetActive(false);
        main_menu.SetActive(false);

        // state is general layout at beginning
        state = "gen";

        hold = false;
        selected = null;

        action_map = new ActionsMap();
    }

    // Update is called once per frame
    void Update()
    {

        // set active menu buttons based on how many are put down in the scene
        if (GameObject.FindGameObjectsWithTag("mp_ball").Length > 0) {
            ball_button.SetActive(false);
        } else {
            ball_button.SetActive(true);
        }

        if (GameObject.FindGameObjectsWithTag("mp_hole").Length > 0) {
            hole_button.SetActive(false);
        } else {
            hole_button.SetActive(true);
        }

        if (GameObject.FindGameObjectsWithTag("mp_ref").Length > 0) {
            paper_button.SetActive(false);
        } else {
            paper_button.SetActive(true);
        }

        // set save button to active or not based on if all necessary elements are placed
        if (GameObject.FindGameObjectsWithTag("mp_ball").Length > 0 && GameObject.FindGameObjectsWithTag("mp_hole").Length > 0 && GameObject.FindGameObjectsWithTag("mp_ref").Length > 0) {
            save_button.SetActive(true);
            warning_text.SetActive(false);
        } else {
            save_button.SetActive(false);
            warning_text.SetActive(true);
        }

        // if state gen
        if (state == "gen") {
            // switch action map
            playerInput.SwitchCurrentActionMap("GenMenu");

            // set only gen menu to active
            gen_menu.SetActive(true);
            placement_menu.SetActive(false);
            manip_menu.SetActive(false);
            main_menu.SetActive(false);

            // undo
            if (playerInput.actions["Undo"].triggered) {
                Undo();
            }

            // redo
            if (playerInput.actions["Redo"].triggered) {
                Redo();
            }

            // if something is selected, set the selected object to its original material
            if (selected != null) {
                selected.GetComponent<MeshRenderer>().material = selected_mat;
            }
            
            // main menu button
            if (playerInput.actions["MainMenuButton"].triggered) {
                state = "main";
            }

            // crate button
            if (playerInput.actions["Crate"].triggered) {
                state = "place";
                SetSelected(crate_prefab);
            }

            // bush button
            else if (playerInput.actions["Bush"].triggered) {
                state = "place";
                SetSelected(bush_prefab);
            }

            // slabs button
            else if (playerInput.actions["Slabs"].triggered) {
                state = "place";
                SetSelected(slabs_prefab);
            }

            // rock button
            else if (playerInput.actions["Rock"].triggered) {
                state = "place";
                SetSelected(rock_prefab);
            }

            // golf ball button
            else if (playerInput.actions["GolfBall"].triggered) {
                state = "place";
                SetSelected(golfball_prefab);
            }

            // golf hole button
            else if (playerInput.actions["GolfHole"].triggered) {
                state = "place";
                SetSelected(golfhole_prefab);
            }

            // ref paper button
            else if (playerInput.actions["RefPaper"].triggered) {
                state = "place";
                SetSelected(paper_prefab);
            }

            // touch select
            else {
                Select();
            }
        }

        // if state place
        else if (state == "place") {
            // change input map
            playerInput.SwitchCurrentActionMap("PlacementMenu");

            // set only placement menu to true
            gen_menu.SetActive(false);
            placement_menu.SetActive(true);
            manip_menu.SetActive(false);
            main_menu.SetActive(false);

            if (playerInput.actions["Exit"].triggered) {
                state = "gen";
            }

            if (Input.touchCount == 1) {
                //get touch
                Touch touch = Input.GetTouch(0);

                // only try placing if the touch is a tap
                // (as opposed to a drag/flick)
                if (touch.phase == TouchPhase.Ended)
                {
                    PlaceObject(touch);
                }
            }
        }

        // if state manip
        else if (state == "manip") {

            // swithc input action map
            playerInput.SwitchCurrentActionMap("ManipMenu");

            // set only manip menu to active
            gen_menu.SetActive(false);
            placement_menu.SetActive(false);
            manip_menu.SetActive(true);
            main_menu.SetActive(false);

            // exit button
            if (playerInput.actions["Exit"].triggered) {
                // get all affected objects
                List<GameObject> aff = new List<GameObject>();

                // if it is a crate
                if (selected.tag == "mp_crate") {
                    // look above and below for other crates
                    var ray = new Ray(selected.transform.position, Vector3.up);
                    var above = Physics.RaycastAll(ray);

                    ray = new Ray(selected.transform.position, -Vector3.up);
                    var below = Physics.RaycastAll(ray);

                    // add those crates to the list of affected objects
                    foreach (RaycastHit r in above) {
                        if (r.transform.gameObject.tag == "mp_crate") {
                            aff.Add(r.transform.gameObject);
                        }
                    }

                    foreach (RaycastHit r in below) {
                        if (r.transform.gameObject.tag == "mp_crate") {
                            aff.Add(r.transform.gameObject);
                        }
                    }
                }

                // exit manipulation
                ExitManip("mov", aff);
            }

            // if it is a crate, this is how to interact with it
            if (selected.tag == "mp_crate") {
                ManipStackable();
            }

            // else you can drag it however you like
            else {
                Manip();
            }
        }


        // if state main
        else if (state == "main") {
            // switch input action map
            playerInput.SwitchCurrentActionMap("MainMenu");

            // set only main menu to true
            gen_menu.SetActive(false);
            placement_menu.SetActive(false);
            manip_menu.SetActive(false);
            main_menu.SetActive(true);

            // save button 
            if (playerInput.actions["SaveButton"].triggered) {
                Save();
            }

            // exit main menu button
            if (playerInput.actions["ExitButton"].triggered) {
                state = "gen";
            }

            // reset button
            if (playerInput.actions["ResetButton"].triggered) {
                ResetGame();
            }
        }

    }

    // set selected object
    public void SetSelected (GameObject select) {
        selected = select;
        selected_mat = selected.GetComponent<MeshRenderer>().material;
        selected_position = selected.transform.position;
        selected_rotation = selected.transform.rotation;
        rotate_slider.GetComponent<Slider>().value = 0;

        if (state == "manip") {
            selected.GetComponent<MeshRenderer>().material = highlight;
        } 
    }

    // place object at touch
    private void PlaceObject(Touch touch) {
        // get touch position (2D, on screen)
        Vector2 touchPos = touch.position;

        if (selected.tag == "mp_crate") {

            Ray ray = Camera.main.ScreenPointToRay(touchPos);
            RaycastHit hit_obj;

            if (!Physics.Raycast(ray, out hit_obj)) {
                Place(touchPos);
            } 
            else if (hit_obj.transform.gameObject.tag == "mp_crate") {
                PlaceStackable(hit_obj.transform.gameObject);
            }
            else {
                Place(touchPos);
            }
        } 
        
        else {
            Place(touchPos);
        }

    }

    // to select an object via touch
    private void Select() {
        if (Input.touchCount == 1) {
            // get touch
            Touch touch = Input.GetTouch(0);

            // only try selecting if the touch is a tap
            if (touch.phase == TouchPhase.Ended)
            {
                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit_obj;

                if (Physics.Raycast(ray, out hit_obj)) {
                    if (hit_obj.transform.gameObject.tag.Contains("mp_")) {
                        
                        // enter manip state
                        state = "manip";
                        
                        // set selected
                        SetSelected(hit_obj.transform.gameObject);
                    }
                }
            }
        }
    }

    // manip state functions
    private void Manip() {
        Delete();
        Drag();
        Rotate();
    }

    // manip state functions if stackable is selected
    private void ManipStackable() {
        DeleteStackable();
        DragStackable();
        RotateStackable();
    }

    // leave manip state (add new action to action map)
    private void ExitManip(string t, List<GameObject> a) {
        state = "gen";

        if (t != "del") {
            Action new_action = new Action(selected, selected_position, selected_rotation, selected.transform.position, selected.transform.rotation, "mov", a);
            action_map.AddAction(new_action);
        }
    }

    // for dragging selected object
    private void Drag() {
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) {
                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit_obj;

                if (Physics.Raycast(ray, out hit_obj, MAX_DIST)) {
                    if (hit_obj.transform.gameObject.tag.Contains("mp_") && GameObject.ReferenceEquals(hit_obj.transform.gameObject, selected)) {
                        hold = true;
                    }
                }
            }

            if (touch.phase == TouchPhase.Moved) {
                touch_pos = touch.position;
            }

            if (touch.phase == TouchPhase.Ended) {
                hold = false;
            }
        }

        if (hold) {
            if (raycastManager.Raycast(touch_pos, hits, TrackableType.PlaneWithinPolygon)) {
                
                Pose hit_pose = hits[0].pose;
                Collider[] overlapping = Physics.OverlapBox(hit_pose.position, selected.transform.localScale/2, selected.transform.rotation);
                int count = 0;

                for (int i = 0; i < overlapping.Length; i++) {
                    if (!GameObject.ReferenceEquals(overlapping[i].gameObject, selected) && overlapping[i].gameObject.tag.Contains("mp_")) {
                        count += 1;
                    }
                }
                if (count == 0) {
                    selected.transform.position = hit_pose.position;
                }
            }
        }
    }

    // for deleting selected object
    private void Delete() {
        if (playerInput.actions["Delete"].triggered) {
            // get deleted position and rotation
            GameObject deleted = selected;
            Vector3 del_pos = selected.transform.position;
            Quaternion del_rot = selected.transform.rotation;

            selected.SetActive(false);

            // store in action map
            Action new_action = new Action(deleted, del_pos, del_rot, Vector3.up, Quaternion.identity, "del", new List<GameObject>());
            action_map.AddAction(new_action);

            // exit manip mode
            ExitManip("del", null);
        }
    }

    // for rotating selected object
    private void Rotate() {

        float y_rotation = rotate_slider.GetComponent<Slider>().normalizedValue * 5;
        selected.transform.Rotate(0, y_rotation, 0);

    }

    // for dragging stackable object
    private void DragStackable() {

        // get whole stack above and below anything that is a crate
        var ray = new Ray(selected.transform.position, Vector3.up);
        var above = Physics.RaycastAll(ray);

        ray = new Ray(selected.transform.position, -Vector3.up);
        var below = Physics.RaycastAll(ray);

        // code to register whether touch is held
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) {
                ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit_obj;

                if (Physics.Raycast(ray, out hit_obj, MAX_DIST)) {
                    if (hit_obj.transform.gameObject.tag.Contains("mp_") && GameObject.ReferenceEquals(hit_obj.transform.gameObject, selected)) {
                        hold = true;
                    }
                }
            }

            if (touch.phase == TouchPhase.Moved) {
                touch_pos = touch.position;
            }

            if (touch.phase == TouchPhase.Ended) {
                hold = false;
            }
        }

        // if holding
        if (hold) {
            if (raycastManager.Raycast(touch_pos, hits, TrackableType.PlaneWithinPolygon)) {
                
                Pose hit_pose = hits[0].pose;
                Collider[] overlapping = Physics.OverlapBox(hit_pose.position, selected.transform.localScale/2, selected.transform.rotation);
                int count = 0;

                bool crate = false;

                for (int i = 0; i < overlapping.Length; i++) {
                    if (!GameObject.ReferenceEquals(overlapping[i].gameObject, selected) && overlapping[i].gameObject.tag.Contains("mp_")) {
                        count += 1;
                    }
                }
                if (count == 0) {
                    selected.transform.position = new Vector3(hit_pose.position.x, selected.transform.position.y, hit_pose.position.z);
                   
                    foreach (RaycastHit r in above) {
                        GameObject h = r.transform.gameObject;
                        if (h.tag == "mp_crate") {
                            h.transform.position = new Vector3(hit_pose.position.x, h.transform.position.y, hit_pose.position.z);
                        }
                    }

                    foreach (RaycastHit r in below) {
                        GameObject h = r.transform.gameObject;
                        if (h.tag == "mp_crate") {
                            h.transform.position = new Vector3(hit_pose.position.x, h.transform.position.y, hit_pose.position.z);
                        }
                    }
                }
            }
        }
    }

    // deleting a stackable unit
    private void DeleteStackable() {
        var ray = new Ray(selected.transform.position, Vector3.up);
        var above = Physics.RaycastAll(ray);

        if (playerInput.actions["Delete"].triggered) {

            GameObject deleted = selected;
            Vector3 del_pos = selected.transform.position;
            Quaternion del_rot = selected.transform.rotation;

            selected.SetActive(false);

            List<GameObject> affected_obj = new List<GameObject>();

            foreach (RaycastHit r in above) {
                GameObject h = r.transform.gameObject;
                if (h.tag == "mp_crate") {
                    h.transform.position -= new Vector3(0, selected.transform.localScale.y, 0);
                    affected_obj.Add(h);
                }
            }

            Action new_action = new Action(deleted, del_pos, del_rot, Vector3.up, Quaternion.identity, "del", affected_obj);
            action_map.AddAction(new_action);

            ExitManip("del", affected_obj);
        }
        
    }

    // rotating stacked units
    private void RotateStackable() {
        var ray = new Ray(selected.transform.position, Vector3.up);
        var above = Physics.RaycastAll(ray);

        ray = new Ray(selected.transform.position, -Vector3.up);
        var below = Physics.RaycastAll(ray);

        float y_rotation = rotate_slider.GetComponent<Slider>().normalizedValue * 5;
        selected.transform.Rotate(0, y_rotation, 0);

        foreach (RaycastHit r in above) {
            GameObject h = r.transform.gameObject;
            if (h.tag == "mp_crate") {
                h.transform.rotation = selected.transform.rotation;
            }

        }

        foreach (RaycastHit r in below) {
            GameObject h = r.transform.gameObject;
            if (h.tag == "mp_crate") {
                h.transform.rotation = selected.transform.rotation;
            }       
        }
    }

    // placing regular object
    private void Place(Vector3 pos) {
        if (raycastManager.Raycast(pos, hits, TrackableType.PlaneWithinPolygon)) {
            var hit = hits[0].pose;

            Vector3 try_pos = hit.position;
            bool invalid_pos = true; 
            while (invalid_pos) {
                Collider[] overlapping = Physics.OverlapBox(try_pos, selected.transform.localScale/2, selected.transform.rotation);
                int count = 0;

                for (int i = 0; i < overlapping.Length; i++) {
                    if (!GameObject.ReferenceEquals(overlapping[i].gameObject, selected) && overlapping[i].gameObject.tag.Contains("mp_")) {
                        count += 1;
                    }
                }

                if (count == 0) {
                    GameObject created = Instantiate(selected, try_pos, Quaternion.identity);
                    Action new_action = new Action(created, Vector3.up, Quaternion.identity, created.transform.position, created.transform.rotation, "add", new List<GameObject>());
                    action_map.AddAction(new_action);

                    invalid_pos = false;
                } else {
                    try_pos.x += 0.2f;
                }
            }

            state = "gen";
        }
    }

    // placing stackable cube
    private void PlaceStackable(GameObject parent) {
        Vector3 new_crate_pos = parent.transform.position;
        new_crate_pos.y += parent.transform.localScale.y;

        Vector3 try_pos = new_crate_pos;
        bool invalid_pos = true; 

        while (invalid_pos) {
            Collider[] overlapping = Physics.OverlapBox(try_pos, selected.transform.localScale/2, parent.transform.rotation);
            int count = 0;

            for (int i = 0; i < overlapping.Length; i++) {
                if (!GameObject.ReferenceEquals(overlapping[i].gameObject, selected) && overlapping[i].gameObject.tag.Contains("mp_")) {
                    count += 1;
                }
            }

            if (count == 0) {
                GameObject created = Instantiate(selected, try_pos, parent.transform.rotation);
                Action new_action = new Action(created, Vector3.up, Quaternion.identity, created.transform.position, created.transform.rotation, "add", new List<GameObject>());
                action_map.AddAction(new_action);

                invalid_pos = false;
            } else {
                try_pos.y += 0.001f;
            }
        }

        state = "gen";
    }

    // saving
    private void Save() {
        if (playerInput.actions["SaveButton"].triggered) {

            GameObject[] paper = GameObject.FindGameObjectsWithTag("mp_ref");

            GameObject reference = paper[0];
            var o_list = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            string fileName = Application.persistentDataPath + "/savedefault.csv";
            using (StreamWriter w = new StreamWriter(fileName, false))
            {
                foreach (GameObject o in o_list)
                {
                    GameObject g = (GameObject) o;
                    if (g.tag.Contains("mp_") && g.tag != "mp_ref" && g.activeSelf) {
                        Vector3 rel_pos = g.transform.position - reference.transform.position;
                        float y_rel = g.transform.eulerAngles.y - reference.transform.eulerAngles.y;
                        string type = g.tag;

                        string newline = string.Format("{0},{1},{2},{3},{4}", g.tag, y_rel.ToString(), rel_pos.x.ToString(), rel_pos.y.ToString(), rel_pos.z.ToString());
                        w.WriteLine(newline);

                        Debug.Log("Wrote " + newline +" to file");
                        w.Flush();
                    }
                }
            }
            Debug.Log("Savefile saved to: " + fileName);
            saved_text.SetActive(true);
        }
    }

    // different possible types
    // add
    // del
    // mov

    // to undo add, del gameobj in action
    // to undo del, instantiate gameobj in og_position and og_rotation
    // to undo mov, change gameobj transform position to og_position and rotation to og_rotation

    public void Undo() {
        Action last_action = action_map.GetLastAction();

        if (last_action.type == "add") {
            last_action.manip.SetActive(false);
        } 

        else if (last_action.type == "del") {
            last_action.manip.SetActive(true);

            foreach (GameObject o in last_action.affected) {
                o.transform.position += new Vector3(0, selected.transform.localScale.y, 0);
            }
        }

        else if (last_action.type == "mov") {
            last_action.manip.transform.position = last_action.og_position;
            last_action.manip.transform.rotation = last_action.og_rotation;

            foreach (GameObject o in last_action.affected) {
                o.transform.position = new Vector3(last_action.manip.transform.position.x, o.transform.position.y, last_action.manip.transform.position.z);
                o.transform.rotation = last_action.manip.transform.rotation;
            }
        }

        action_map.DecPointer();
    }


    // to redo add, instantiate gameobj in action at new_position and new_rotation
    // to redo del, setactive false gameobj
    // to redo mov, move gameobj to new_position and new_rotation

    public void Redo() {
        Action next_action = action_map.GetNextAction();

        if (next_action != null) {
            if (next_action.type == "add") {
                next_action.manip.SetActive(true);
            }
            else if (next_action.type == "del") {
                next_action.manip.SetActive(false);
            }
            else if (next_action.type == "mov") {
                next_action.manip.transform.position = next_action.new_position;
                next_action.manip.transform.rotation = next_action.new_rotation;

                foreach (GameObject o in next_action.affected) {
                    o.transform.position = new Vector3(next_action.manip.transform.position.x, o.transform.position.y, next_action.manip.transform.position.z);
                    o.transform.rotation = next_action.manip.transform.rotation;
                }
            }
        }

        action_map.IncPointer();
    }

    // reset game
    public void ResetGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
