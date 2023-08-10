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

        // public float tileWidth = 5f;

        [Header("OffMeshLinks")]
        public float maxJumpHeight = 2f;
        public float maxJumpDist = 2f;
        public LayerMask raycastLayerMask = -1;
        public float sphereCastRadius = 2f;

        //how far over to move spherecast away from navmesh edge to prevent detecting the same edge
        public float cheatOffset = 0.25f;

        //how high up to bump raycasts to check for walls (to prevent forming links through walls)
        public float wallCheckYOffset = 0.5f;

        [Header("EdgeNormal")] public bool invertFacingNormal = false;
        public bool dontAllignYAxis = false;


        //private List< Vector3 > spawnedLinksPositionsList = new List< Vector3 >();


        #endregion


        #region GridGen

        public void Generate(API.Agent agent, float edgeDivisionWidth) {

            // Clear old visualizers Todo: Remove
            foreach (var lineRenderer in Visulizers) {
                Destroy(lineRenderer);
            }
            Visulizers.Clear();
            ignoredNotAgentNavMesh = 0;
            ignoredHadColliderBetween = 0;

            MelonLogger.Msg("Generating Mesh Links...");
            MelonLogger.Msg("\tCalculating Edges...");
            var edges = CalcEdges();


            MelonLogger.Msg("\tPlacing Tiles");

            PlaceTiles(agent, edgeDivisionWidth, edges);
            MelonLogger.Msg($"\tDone! ignoredNotAgentNavMesh: {ignoredNotAgentNavMesh}, ignoredHadColliderBetween: {ignoredHadColliderBetween}");
        }

        private void PlaceTiles(API.Agent agent, float edgeDivisionWidth, List<Edge> edges) {
            // ClearLinks(agent);

            foreach (var edge in edges) {
                // var tilesCountWidth = (int) Mathf.Clamp(edge.length / tileWidth, 0, 10000);
                var tilesCountWidth = (int) Mathf.Clamp(edge.length / edgeDivisionWidth, 0, 10000);
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

            // Check if we're on the navmesh of our agent Todo: Is this relevant?
            if (!NavMesh.SamplePosition(pos, out _, 0.0000001f,
                    new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) {
                ignoredNotAgentNavMesh++;
                return;
            }

            var startPos = pos + normal * Vector3.forward * agent.Settings.agentRadius * 2;

            // The end pos is just down for slightly more than the max Jump Height
            var endPos = startPos - Vector3.up * maxJumpHeight * 1.1f;

            //Debug.DrawLine ( pos + Vector3.right * 0.2f, endPos, Color.white, 2 );

            // Look for a collider on the edge normal direction
            if (!Physics.Linecast(startPos, endPos, out var raycastHit, raycastLayerMask.value, QueryTriggerInteraction.Ignore)) return;

            // Check if there's a nav mesh within the line cast hit
            if (!NavMesh.SamplePosition(raycastHit.point, out var navMeshHit, 1f, new NavMeshQueryFilter { agentTypeID = agent.AgentTypeID, areaMask = NavMesh.AllAreas })) return;

            //Debug.DrawLine( pos, navMeshHit.position, Color.black, 15 );

            // if (Vector3.Distance(pos, navMeshHit.position) <= 1.1f) return;
            // Ignore heights that the agent can climb
            // if (Vector3.Distance(pos, navMeshHit.position) <= agent.Settings.agentClimb) return;

            //added these 2 line to check to make sure there aren't flat horizontal links going through walls
            var calcV3 = (pos - normal * Vector3.forward * 0.02f);

            // Ignore if our agent can climb this, as it should be taken care
            if (Mathf.Abs(calcV3.y - navMeshHit.position.y) < agent.Settings.agentClimb) return;

            // Check if there's a collider between the points at the starting height
            var posABitHigher = pos with { y = pos.y + 0.1f };
            var directionHorizontal = raycastHit.point with { y = posABitHigher.y } - posABitHigher;
            if (Physics.Raycast(posABitHigher, directionHorizontal.normalized, directionHorizontal.magnitude, 1 << NavMeshTools.DefaultLayer)) {
                // Create new visualizers
                CreateVisualizer(Color.red, true, posABitHigher, raycastHit.point with { y = posABitHigher.y }, 0.025f);
                ignoredHadColliderBetween++;
                return;
            }

            // Send the nav mesh link to the bake
            NavMeshTools.Instance.AddNavMeshLink(agent, new NavMeshLinkData() {
                // startPosition = calcV3,
                startPosition = pos,
                endPosition = navMeshHit.position,
                width = agent.Settings.agentRadius,
                costModifier = 10f,
                bidirectional = true,
                area = 2,
                agentTypeID = agent.AgentTypeID,
            });

            // Create new visualizers
            CreateVisualizer(Color.green, true, calcV3, navMeshHit.position);
        }

        private int ignoredNotAgentNavMesh = 0;
        private int ignoredHadColliderBetween = 0;

        private void CheckPlacePosHorizontal(Vector3 pos, Quaternion normal, API.Agent agent) {

            var startPos = pos + normal * Vector3.forward * agent.Settings.agentRadius * 2;
            var endPos = startPos - normal * Vector3.back * maxJumpDist * 1.1f;

            // Cheat forward a little bit so the sphereCast doesn't touch this ledge.
            var cheatStartPos = LerpByDistance(startPos, endPos, cheatOffset);
            //Debug.DrawRay(endPos, Vector3.up, Color.blue, 2);
            //Debug.DrawLine ( cheatStartPos , endPos, Color.white, 2 );
            //Debug.DrawLine(startPos, endPos, Color.white, 2);

            //calculate direction for Spherecast
            var reUsableV3 = endPos - startPos;
            // raise up pos Y value slightly up to check for wall/obstacle
            var offSetPosY = pos with { y = pos.y + wallCheckYOffset };

            // ray cast to check for walls
            if (Physics.Raycast(offSetPosY, reUsableV3, (maxJumpDist / 2), raycastLayerMask.value)) return;
            //Debug.DrawRay(pos, ReUsableV3, Color.yellow, 15);

            var reverseRayCastSpot = offSetPosY + reUsableV3;

            //now raycast back the other way to make sure we're not raycasting through the inside of a mesh the first time.
            if (Physics.Raycast(reverseRayCastSpot, -reUsableV3, (maxJumpDist + 1), raycastLayerMask.value)) return;
            //Debug.DrawRay(ReverseRayCastSpot, -ReUsableV3, Color.red, 15);
            //Debug.DrawRay(ReverseRayCastSpot, -ReUsableV3, Color.red, 15);

            // if no walls 1 unit out then check for other colliders using the Cheat offset so as to not detect the edge we are spherecasting from.
            if (!Physics.SphereCast(cheatStartPos, sphereCastRadius, reUsableV3, out var raycastHit, maxJumpDist, raycastLayerMask.value, QueryTriggerInteraction.Ignore)) return;
            // if (Physics.Linecast(startPos, endPos, out raycastHit, raycastLayerMask.value, QueryTriggerInteraction.Ignore))

            var cheatRaycastHit = LerpByDistance(raycastHit.point, endPos, .2f);

            if (!NavMesh.SamplePosition(cheatRaycastHit, out var navMeshHit, 1f, new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) return;
            // Debug.DrawLine( pos, navMeshHit.position, Color.black, 15 );

            // Todo: Figure out what this is
            // if ((Vector3.Distance(pos, navMeshHit.position) < 1.1f)) return;
            // if (Mathf.Abs(Vector3.Distance(pos, navMeshHit.position)) <= agent.Settings.agentClimb) return;

            var startingPos = pos - normal * Vector3.forward * 0.02f;

            // Send the nav mesh link to the bake
            NavMeshTools.Instance.AddNavMeshLink(agent, new NavMeshLinkData() {
                startPosition = startingPos,
                endPosition = navMeshHit.position,
                width = agent.Settings.agentRadius,
                costModifier = 10f,
                bidirectional = true,
                area = 2,
                agentTypeID = agent.AgentTypeID,
            });

            // Create new visualizers
            CreateVisualizer(Color.cyan, true, startingPos, navMeshHit.position);
        }

        private void CreateVisualizer(Color color, bool constantWidth, Vector3 start, Vector3 end, float width = 0.1f) {
            // Create new visualizers
            var vis = new GameObject("vis");
            var lineRenderer = vis.AddComponent<LineRenderer>();
            // Set line color
            lineRenderer.material.shader = Shader.Find("Unlit/Color");
            lineRenderer.material.color = color;

            // Set width (you can modify this as needed)
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = constantWidth ? width : width/10;

            // Set positions
            lineRenderer.positionCount = 2;  // We are defining a line with 2 points.
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            Visulizers.Add(lineRenderer);
        }


        #endregion

        //Just a helper function I added to calculate a point between normalized distance of two V3s
        private static Vector3 LerpByDistance(Vector3 a, Vector3 b, float x) {
            return x * Vector3.Normalize(b - a) + a;
        }


        #region EdgeGen

        private const float TriggerAngle = 0.999f;

        private List<Edge> CalcEdges() {
            var edges = new List<Edge>();
            var tr = NavMesh.CalculateTriangulation();

            var currentMesh = new Mesh() {
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

                TrisToEdge(edges, currentMesh, i, i + 1);
                TrisToEdge(edges, currentMesh, i + 1, i + 2);
                TrisToEdge(edges, currentMesh, i + 2, i);
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

            return edges;
        }

        private void TrisToEdge(List<Edge> edges, Mesh currMesh, int n1, int n2) {
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
