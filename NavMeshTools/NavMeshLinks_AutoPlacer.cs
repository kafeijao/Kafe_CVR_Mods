using Kafe.NavMeshTools;
using MelonLoader;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace eDmitriyAssets.NavmeshLinksGenerator {

    public class NavMeshLinks_AutoPlacer : MonoBehaviour {

        // Visualizers
        private HashSet<LineRenderer> Visulizers = new();

        #region Variables

        public float tileWidth = 5f;

        [Header("OffMeshLinks")]
        public float maxJumpHeight = 3f;
        public float maxJumpDist = 3f;
        public LayerMask raycastLayerMask = -1;
        public float sphereCastRadius = 2f;

        //how far over to move spherecast away from navmesh edge to prevent detecting the same edge
        public float cheatOffset = 0.25f;

        //how high up to bump raycasts to check for walls (to prevent forming links through walls)
        public float wallCheckYOffset = 0.5f;

        [Header("EdgeNormal")] public bool invertFacingNormal = false;
        public bool dontAllignYAxis = false;


        //private List< Vector3 > spawnedLinksPositionsList = new List< Vector3 >();
        private Mesh currentMesh;
        private List<Edge> edges = new List<Edge>();

        private Vector3 _reUsableV3;
        private Vector3 _offSetPosY;

        #endregion


        #region GridGen

        public void Generate(API.Agent agent) {

            // Clear old visualizers Todo: Remove
            foreach (var lineRenderer in Visulizers) {
                Destroy(lineRenderer);
            }
            Visulizers.Clear();

            edges.Clear();
            //spawnedLinksPositionsList.Clear();

            CalcEdges();
            PlaceTiles(agent);
        }


        public void ClearLinks(API.Agent agent) {
            var navMeshLinkList = GetComponentsInChildren<NavMeshLink>().ToList();
            while (navMeshLinkList.Count > 0) {
                if (navMeshLinkList[0].agentTypeID == agent.AgentTypeID) {
                    var obj = navMeshLinkList[0].gameObject;
                    if (obj != null) DestroyImmediate(obj);
                }
                navMeshLinkList.RemoveAt(0);
            }
        }

        private void PlaceTiles(API.Agent agent) {
            if (edges.Count == 0) return;

            ClearLinks(agent);

            foreach (var edge in edges) {
                var tilesCountWidth = (int) Mathf.Clamp(edge.length / tileWidth, 0, 10000);
                float heightShift = 0;

                for (var columnN = 0; columnN < tilesCountWidth; columnN++) { //every edge length segment
                    var placePos = Vector3.Lerp(
                                           edge.start,
                                           edge.end,
                                           columnN / (float)tilesCountWidth //position on edge
                                           + 0.5f / tilesCountWidth //shift for half tile width
                                       ) + edge.facingNormal * Vector3.up * heightShift;

                    //spawn up/down links
                    CheckPlacePos(placePos, edge.facingNormal, agent);
                    //spawn horizontal links
                    CheckPlacePosHorizontal(placePos, edge.facingNormal, agent);
                }
            }
        }




        private void CheckPlacePos(Vector3 pos, Quaternion normal, API.Agent agent) {

            var startPos = pos + normal * Vector3.forward * agent.Settings.agentRadius * 2;
            var endPos = startPos - Vector3.up * maxJumpHeight * 1.1f;

            //Debug.DrawLine ( pos + Vector3.right * 0.2f, endPos, Color.white, 2 );

            if (!Physics.Linecast(startPos, endPos, out var raycastHit, raycastLayerMask.value, QueryTriggerInteraction.Ignore)) return;
            if (!NavMesh.SamplePosition(raycastHit.point, out var navMeshHit, 1f, new NavMeshQueryFilter { agentTypeID = agent.AgentTypeID, areaMask = NavMesh.AllAreas })) return;
            //Debug.DrawLine( pos, navMeshHit.position, Color.black, 15 );

            // if (Vector3.Distance(pos, navMeshHit.position) <= 1.1f) return;
            // Ignore heights that the agent can climb
            if (Vector3.Distance(pos, navMeshHit.position) <= agent.Settings.agentClimb) return;

            //added these 2 line to check to make sure there aren't flat horizontal links going through walls
            var calcV3 = (pos - normal * Vector3.forward * 0.02f);

            // Todo: Figure out what this is
            // if ((calcV3.y - navMeshHit.position.y) <= 1f) return;
            // if (Mathf.Abs(calcV3.y - navMeshHit.position.y) < agent.Settings.agentClimb) return;

            // //SPAWN NAVMESH LINKS
            // Transform spawnedTransf = Instantiate(
            //     linkPrefab.transform,
            //     //pos - normal * Vector3.forward * 0.02f,
            //     calcV3,
            //     normal
            // );

            // // Spawn and setup the NavMeshLink Todo: Investigate if NavMeshLink_TBS brings something or we just use NavMeshLink
            // var spawnedGo = new GameObject("NM_Link");
            // var spawnedTransf = spawnedGo.transform;
            // var navMeshLinkTbs = spawnedGo.AddComponent<NavMeshLink_TBS>();
            // navMeshLinkTbs.animation_FromStart = "JumpDown";
            // navMeshLinkTbs.animation_FromEnd = "JumpUp";
            //
            // // Setup Nav Mesh Link stuff
            // var nmLink = spawnedTransf.GetComponent<NavMeshLink>();
            // nmLink.startPoint = Vector3.zero;
            // nmLink.endPoint = nmLink.transform.InverseTransformPoint(navMeshHit.position);
            // nmLink.autoUpdate = true;
            // nmLink.agentTypeID = agent.AgentTypeID;
            // nmLink.costModifier = 2;
            // nmLink.area = 2;
            // // Todo: Check if these are decent values
            // nmLink.width = 0.5f;
            // nmLink.bidirectional = true;
            //
            // nmLink.UpdateLink();
            //
            // spawnedTransf.SetParent(transform);

            // Send the nav mesh link to the bake
            NavMeshTools.Instance.AddNavMeshLink(agent, new NavMeshLinkData() {
                startPosition = calcV3,
                endPosition = navMeshHit.position,
                width = agent.Settings.agentRadius,
                costModifier = 2f,
                bidirectional = true,
                area = 2,
                agentTypeID = agent.AgentTypeID,
            });

            // Create new visualizers
            var vis = new GameObject("vis");
            var lineRenderer = vis.AddComponent<LineRenderer>();
            // Set line color to green
            lineRenderer.material.shader = Shader.Find("Unlit/Color");
            lineRenderer.material.color = Color.green;

            // Set width (you can modify this as needed)
            lineRenderer.startWidth = lineRenderer.endWidth = 0.05f;

            // Set positions
            lineRenderer.positionCount = 2;  // We are defining a line with 2 points.
            lineRenderer.SetPosition(0, calcV3);
            lineRenderer.SetPosition(1, navMeshHit.position);
            Visulizers.Add(lineRenderer);
        }

        private void CheckPlacePosHorizontal(Vector3 pos, Quaternion normal, API.Agent agent) {

            var startPos = pos + normal * Vector3.forward * agent.Settings.agentRadius * 2;
            var endPos = startPos - normal * Vector3.back * maxJumpDist * 1.1f;
            // Cheat forward a little bit so the sphereCast doesn't touch this ledge.
            var cheatStartPos = LerpByDistance(startPos, endPos, cheatOffset);
            //Debug.DrawRay(endPos, Vector3.up, Color.blue, 2);
            //Debug.DrawLine ( cheatStartPos , endPos, Color.white, 2 );
            //Debug.DrawLine(startPos, endPos, Color.white, 2);

            //calculate direction for Spherecast
            _reUsableV3 = endPos - startPos;
            // raise up pos Y value slightly up to check for wall/obstacle
            _offSetPosY = new Vector3(pos.x, (pos.y + wallCheckYOffset), pos.z);

            // ray cast to check for walls
            if (Physics.Raycast(_offSetPosY, _reUsableV3, (maxJumpDist / 2), raycastLayerMask.value)) return;
            //Debug.DrawRay(pos, ReUsableV3, Color.yellow, 15);

            var reverseRayCastSpot = (_offSetPosY + (_reUsableV3));

            //now raycast back the other way to make sure we're not raycasting through the inside of a mesh the first time.
            if (Physics.Raycast(reverseRayCastSpot, -_reUsableV3, (maxJumpDist + 1), raycastLayerMask.value)) return;
            //Debug.DrawRay(ReverseRayCastSpot, -ReUsableV3, Color.red, 15);
            //Debug.DrawRay(ReverseRayCastSpot, -ReUsableV3, Color.red, 15);

            // if no walls 1 unit out then check for other colliders using the Cheat offset so as to not detect the edge we are spherecasting from.
            if (!Physics.SphereCast(cheatStartPos, sphereCastRadius, _reUsableV3, out var raycastHit, maxJumpDist, raycastLayerMask.value, QueryTriggerInteraction.Ignore)) return;
            // if (Physics.Linecast(startPos, endPos, out raycastHit, raycastLayerMask.value, QueryTriggerInteraction.Ignore))

            var cheatRaycastHit = LerpByDistance(raycastHit.point, endPos, .2f);

            if (!NavMesh.SamplePosition(cheatRaycastHit, out var navMeshHit, 1f, new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) return;
            // Debug.Log("Success");
            // Debug.DrawLine( pos, navMeshHit.position, Color.black, 15 );

            // Todo: Figure out what this is
            // if ((Vector3.Distance(pos, navMeshHit.position) < 1.1f)) return;
            // if (Mathf.Abs(Vector3.Distance(pos, navMeshHit.position)) <= agent.Settings.agentClimb) return;

            //SPAWN NAVMESH LINKS
            // Transform spawnedTransf = Instantiate(
            //     OnewayLinkPrefab.transform,
            //     pos - normal * Vector3.forward * 0.02f,
            //     normal
            // ) as Transform;

            // // Spawn and setup the NavMeshLink Todo: Investigate if NavMeshLink_TBS brings something or we just use NavMeshLink
            // var spawnedGo = new GameObject("NM_Link");
            // var spawnedTransf = spawnedGo.transform;
            // var navMeshLinkTbs = spawnedGo.AddComponent<NavMeshLink_TBS>();
            // navMeshLinkTbs.animation_FromStart = "JumpDown";
            // navMeshLinkTbs.animation_FromEnd = "JumpUp";
            //
            // // Setup Nav Mesh Link stuff
            // var nmLink = spawnedTransf.GetComponent<NavMeshLink>();
            //
            // nmLink.autoUpdate = true;
            // nmLink.agentTypeID = agent.AgentTypeID;
            // nmLink.costModifier = 2;
            // nmLink.area = 2;
            // // Todo: Check if these are decent values
            // nmLink.width = 0.5f;
            // nmLink.bidirectional = true;
            //
            // nmLink.UpdateLink();
            //
            // spawnedTransf.SetParent(transform);


            var startingPos = pos - normal * Vector3.forward * 0.02f;

            // Send the nav mesh link to the bake
            NavMeshTools.Instance.AddNavMeshLink(agent, new NavMeshLinkData() {
                startPosition = startingPos,
                endPosition = navMeshHit.position,
                width = agent.Settings.agentRadius,
                costModifier = 2f,
                bidirectional = true,
                area = 2,
                agentTypeID = agent.AgentTypeID,
            });

            // Create new visualizers
            var vis = new GameObject("vis");
            var lineRenderer = vis.AddComponent<LineRenderer>();
            // Set line color to green
            lineRenderer.material.shader = Shader.Find("Unlit/Color");
            lineRenderer.material.color = Color.cyan;

            // Set width (you can modify this as needed)
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.01f;

            // Set positions
            lineRenderer.positionCount = 2;  // We are defining a line with 2 points.
            lineRenderer.SetPosition(0, startingPos);
            lineRenderer.SetPosition(1, navMeshHit.position);
            Visulizers.Add(lineRenderer);
        }


        #endregion

        //Just a helper function I added to calculate a point between normalized distance of two V3s
        private static Vector3 LerpByDistance(Vector3 a, Vector3 b, float x) {
            var p = x * Vector3.Normalize(b - a) + a;
            return p;
        }


        #region EdgeGen

        private const float TriggerAngle = 0.999f;

        private void CalcEdges() {
            var tr = NavMesh.CalculateTriangulation();

            currentMesh = new Mesh() {
                vertices = tr.vertices,
                triangles = tr.indices
            };

            // Create navmesh visualization Todo: Remove
            if (!TryGetComponent<MeshFilter>(out var meshFilter)) {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (!TryGetComponent<MeshRenderer>(out var meshRenderer)) {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
            meshFilter.mesh = currentMesh;
            var transparentMat = new Material(Shader.Find("Standard"));
            transparentMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            transparentMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            transparentMat.SetInt("_ZWrite", 0);
            transparentMat.DisableKeyword("_ALPHATEST_ON");
            transparentMat.DisableKeyword("_ALPHABLEND_ON");
            transparentMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            transparentMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            transparentMat.color = new Color(0.2f, 0.5f, 1f, 0.5f);
            meshRenderer.material = transparentMat;


            for (var i = 0; i < currentMesh.triangles.Length - 1; i += 3) {
                //CALC FROM MESH OPEN EDGES vertices

                TrisToEdge(currentMesh, i, i + 1);
                TrisToEdge(currentMesh, i + 1, i + 2);
                TrisToEdge(currentMesh, i + 2, i);
            }

            foreach (var edge in edges) {
                //EDGE LENGTH
                edge.length = Vector3.Distance(
                    edge.start,
                    edge.end
                );

                //FACING NORMAL
                if (!edge.facingNormalCalculated) {
                    edge.facingNormal = Quaternion.LookRotation(Vector3.Cross(edge.end - edge.start, Vector3.up));

                    if (edge.startUp.sqrMagnitude > 0) {
                        var vect = Vector3.Lerp(edge.endUp, edge.startUp, 0.5f) - Vector3.Lerp(edge.end, edge.start, 0.5f);
                        edge.facingNormal = Quaternion.LookRotation(Vector3.Cross(edge.end - edge.start, vect));

                        //FIX FOR NORMALs POINTING DIRECT TO UP/DOWN
                        if (Mathf.Abs(Vector3.Dot(Vector3.up, (edge.facingNormal * Vector3.forward).normalized)) > TriggerAngle) {
                            edge.startUp += new Vector3(0, 0.1f, 0);
                            vect = Vector3.Lerp(edge.endUp, edge.startUp, 0.5f) - Vector3.Lerp(edge.end, edge.start, 0.5f);
                            edge.facingNormal = Quaternion.LookRotation(Vector3.Cross(edge.end - edge.start, vect));
                        }
                    }

                    if (dontAllignYAxis) {
                        edge.facingNormal = Quaternion.LookRotation(
                            edge.facingNormal * Vector3.forward,
                            Quaternion.LookRotation(edge.end - edge.start) * Vector3.up
                        );
                    }

                    edge.facingNormalCalculated = true;
                }

                if (invertFacingNormal) edge.facingNormal = Quaternion.Euler(Vector3.up * 180) * edge.facingNormal;
            }
        }

        private void TrisToEdge(Mesh currMesh, int n1, int n2) {
            var val1 = currMesh.vertices[currMesh.triangles[n1]];
            var val2 = currMesh.vertices[currMesh.triangles[n2]];

            var newEdge = new Edge(val1, val2);

            //remove duplicate edges
            foreach (var edge in edges) {
                if ((edge.start == val1 & edge.end == val2) || (edge.start == val2 & edge.end == val1)) {
                    //print("Edges duplicate " + newEdge.start + " " + newEdge.end);
                    edges.Remove(edge);
                    return;
                }
            }

            edges.Add(newEdge);
        }

        #endregion
    }

    [Serializable]
    public class Edge
    {
        public Vector3 start;
        public Vector3 end;

        public Vector3 startUp;
        public Vector3 endUp;

        public float length;
        public Quaternion facingNormal;
        public bool facingNormalCalculated = false;


        public Edge(Vector3 startPoint, Vector3 endPoint)
        {
            start = startPoint;
            end = endPoint;
        }
    }
}
