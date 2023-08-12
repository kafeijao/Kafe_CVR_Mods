using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshTools;

public class NavMeshLinksGenerator : MonoBehaviour {

    #if DEBUG
    // Visualizers
    private int ignoredNotAgentNavMesh = 0;
    private int ignoredHadColliderBetween = 0;
    #endif

    #region Variables

    [Header("OffMeshLinks")]
    public float maxJumpHeight = 2f;
    public float maxJumpDist = 2f;
    // private readonly LayerMask raycastLayerMask = -1;
    private readonly LayerMask _raycastLayerMask = GetLayerMask();
    public float sphereCastRadius = 2f;

    //how far over to move spherecast away from navmesh edge to prevent detecting the same edge
    private const float CheatOffset = 0.25f;

    [Header("EdgeNormal")]
    public bool invertFacingNormal = false;
    public bool dontAlignYAxis = false;

    #endregion


    private static int GetLayerMask() {

        // A mask that has all bits set (all layers selected) (-1)
        const int allLayers = ~0;

        // Add each layer to the mask
        var mask = 0;
        mask |= (1 << NavMeshTools.UILayer);
        mask |= (1 << NavMeshTools.UIInternalLayer);
        mask |= (1 << NavMeshTools.PlayerCloneLayer);
        mask |= (1 << NavMeshTools.PlayerLocalLayer);
        mask |= (1 << NavMeshTools.PlayerNetworkLayer);
        mask |= (1 << NavMeshTools.IgnoreRaycastLayer);
        mask |= (1 << NavMeshTools.MirrorReflectionLayer);

        // Now invert the mask to exclude the layers you've added
        return allLayers & ~mask;
    }


    #region GridGen

    public (HashSet<NavMeshLinkData> navMeshLinkResults, HashSet<LinkVisualizer> navMeshLinkVisualizers) Generate(API.Agent agent, Mesh navMeshTriangulation) {

        #if DEBUG
            ignoredNotAgentNavMesh = 0;
            ignoredHadColliderBetween = 0;
        #endif

        var navMeshLinksData = new HashSet<NavMeshLinkData>();
        var linkVisualizers = new HashSet<LinkVisualizer>();

        MelonLogger.Msg("[Thread] Generating Mesh Links...");
        MelonLogger.Msg("\tCalculating Edges...");
        var edges = CalcEdges(navMeshTriangulation);

        MelonLogger.Msg("\tPlacing Tiles");

        var edgeDivisionWidth = agent.Settings.agentRadius * 2;

        PlaceTiles(navMeshLinksData, linkVisualizers, agent, edgeDivisionWidth, edges);

        #if DEBUG
            MelonLogger.Msg($"\tDone! ignoredNotAgentNavMesh: {ignoredNotAgentNavMesh}, ignoredHadColliderBetween: {ignoredHadColliderBetween}");
        #endif

        return (navMeshLinksData, linkVisualizers);
    }

    private void PlaceTiles(HashSet<NavMeshLinkData> results, HashSet<LinkVisualizer> linkVisualizers, API.Agent agent, float edgeDivisionWidth, List<Edge> edges) {

        foreach (var edge in edges) {
            // var tilesCountWidth = (int)Mathf.Clamp(edge.Length / edgeDivisionWidth, 0, 10000);
            var tilesCountWidth = (int)Mathf.Clamp(edge.Length / edgeDivisionWidth, 1, 10000);
            float heightShift = 0;

            for (var columnN = 0; columnN < tilesCountWidth; columnN++) {
                //every edge length segment
                var placePos = Vector3.Lerp(
                    edge.Start,
                    edge.End,
                    columnN / (float)tilesCountWidth //position on edge
                    + 0.5f / tilesCountWidth //shift for half tile width
                ) + edge.FacingNormal * Vector3.up * heightShift;

                // Spawn up/down links
                CheckPlacePos(results, linkVisualizers, placePos, edge.FacingNormal, agent);
                // Spawn horizontal links Todo: Fix the algo
                // CheckPlacePosHorizontal(results, linkVisualizers, placePos, edge.FacingNormal, agent);
            }
        }
    }


    private void CheckPlacePos(HashSet<NavMeshLinkData> results, HashSet<LinkVisualizer> linkVisualizers, Vector3 pos, Quaternion normal, API.Agent agent) {

        // Check if we're on the navmesh of our agent
        if (!NavMesh.SamplePosition(pos, out _, 0.0000001f, new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) {
            #if DEBUG
            ignoredNotAgentNavMesh++;
            #endif
            return;
        }

        var startPos = pos + normal * Vector3.forward * agent.Settings.agentRadius * 2;

        // The end pos is just down for slightly more than the max Jump Height
        var endPos = startPos - Vector3.up * maxJumpHeight * 1.1f;

        //Debug.DrawLine ( pos + Vector3.right * 0.2f, endPos, Color.white, 2 );

        // Look for a collider on the edge normal direction
        if (!Physics.Linecast(startPos, endPos, out var raycastHit, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
            return;
        }

        // Check if there's a nav mesh within the line cast hit
        if (!NavMesh.SamplePosition(raycastHit.point, out var navMeshHit, 1f, new NavMeshQueryFilter { agentTypeID = agent.AgentTypeID, areaMask = NavMesh.AllAreas })) return;

        //Debug.DrawLine( pos, navMeshHit.position, Color.black, 15 );

        //added these 2 line to check to make sure there aren't flat horizontal links going through walls
        var calcV3 = (pos - normal * Vector3.forward * 0.02f);

        // Ignore if our agent can climb this, as it should be taken care
        if (Mathf.Abs(calcV3.y - navMeshHit.position.y) < agent.Settings.agentClimb) return;

        // Check if there's a collider between the points at the starting height
        var posABitHigher = pos with { y = pos.y + 0.1f };
        var directionHorizontal = raycastHit.point with { y = posABitHigher.y } - posABitHigher;
        if (Physics.Raycast(posABitHigher, directionHorizontal.normalized, directionHorizontal.magnitude, _raycastLayerMask, QueryTriggerInteraction.Ignore)) {
            #if DEBUG
            linkVisualizers.Add(new LinkVisualizer(Color.red, true, posABitHigher, raycastHit.point with { y = posABitHigher.y }, 0.025f));
            ignoredHadColliderBetween++;
            #endif
            return;
        }

        // Send the nav mesh link to the bake
        results.Add(new NavMeshLinkData() {
            // startPosition = calcV3,
            startPosition = pos,
            endPosition = navMeshHit.position,
            width = agent.Settings.agentRadius,
            costModifier = 10f,
            bidirectional = true,
            area = 2,
            agentTypeID = agent.AgentTypeID,
        });

        #if DEBUG
        linkVisualizers.Add(new LinkVisualizer(Color.green, true, calcV3, navMeshHit.position));
        #endif
    }

    private void CheckPlacePosHorizontal(HashSet<NavMeshLinkData> results, HashSet<LinkVisualizer> linkVisualizers, Vector3 pos, Quaternion normal, API.Agent agent) {

        // Check if we're on the navmesh of our agent
        if (!NavMesh.SamplePosition(pos, out _, 0.0000001f, new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) {
            #if DEBUG
            ignoredNotAgentNavMesh++;
            #endif
            return;
        }

        var startPos = pos + normal * Vector3.forward * agent.Settings.agentRadius * 2;
        var endPos = startPos - normal * Vector3.back * maxJumpDist * 1.1f;

        // Cheat forward a little bit so the sphereCast doesn't touch this ledge.
        // var cheatStartPos = LerpByDistance(startPos, endPos, cheatOffset);
        //Debug.DrawRay(endPos, Vector3.up, Color.blue, 2);
        //Debug.DrawLine ( cheatStartPos , endPos, Color.white, 2 );
        //Debug.DrawLine(startPos, endPos, Color.white, 2);

        // Get the highest Y and add our agent's height to it, so see if it would be able to jump it
        var highestY = (startPos.y > endPos.y ? startPos : endPos).y + agent.Settings.agentHeight;
        var startToEndHorizontalDir = startPos with { y = highestY } - endPos with { y = highestY };

        // // Raise up pos Y value slightly up to check for wall/obstacle
        // var offSetPosY = pos with { y = pos.y + wallCheckYOffset };
        //
        // // Ray cast to check for walls
        // // if (Physics.Raycast(offSetPosY, sphereCastDirection, (maxJumpDist / 2), raycastLayerMask.value)) return;
        // if (Physics.Raycast(offSetPosY, castDirection, (maxJumpDist / 2), raycastLayerMask.value)) return;
        //
        // var reverseRayCastSpot = offSetPosY + castDirection;
        //
        // //now raycast back the other way to make sure we're not raycasting through the inside of a mesh the first time.
        // if (Physics.Raycast(reverseRayCastSpot, -castDirection, (maxJumpDist + 1), raycastLayerMask.value)) return;
        // //Debug.DrawRay(ReverseRayCastSpot, -ReUsableV3, Color.red, 15);
        // //Debug.DrawRay(ReverseRayCastSpot, -ReUsableV3, Color.red, 15);


        // Calculate direction for Physics cast
        var castDirection = endPos - startPos;
        var capsuleRadius = agent.Settings.agentRadius * 2 - 0.01f;

        // // Look for a collider to jump to
        // if (!Physics.CapsuleCast(startPos, endPos, capsuleRadius, castDirection, out var raycastHit, castDirection.magnitude, _raycastLayerMask, QueryTriggerInteraction.Ignore)) {
        //     #if DEBUG
        //     linkVisualizers.Add(new LinkVisualizer(Color.magenta, true, startPos, endPos with { y = highestY }, 0.02f));
        //     #endif
        //     // Todo: If this fails try a sphere cast?
        //     return;
        // }

        // if no walls 1 unit out then check for other colliders using the Cheat offset so as to not detect the edge we are spherecasting from.
        var cheatStartPos = LerpByDistance(startPos, endPos, CheatOffset);
        if (!Physics.SphereCast(cheatStartPos, sphereCastRadius, castDirection, out var raycastHit, maxJumpDist, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
            #if DEBUG
            linkVisualizers.Add(new LinkVisualizer(Color.magenta, true, startPos, endPos, 0.02f));
            #endif
            return;
        }
        // if (Physics.Linecast(startPos, endPos, out raycastHit, raycastLayerMask.value, QueryTriggerInteraction.Ignore))



        // Check if there's a collider between the points at the highest height (from start to end)
        if (Physics.Raycast(startPos with { y = highestY }, startToEndHorizontalDir.normalized, startToEndHorizontalDir.magnitude, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
            #if DEBUG
            linkVisualizers.Add(new LinkVisualizer(Color.red, false, startPos with { y = highestY }, endPos with { y = highestY }, 0.025f));
            ignoredHadColliderBetween++;
            #endif
            return;
        }
        // Check if there's a collider between the points at the highest height (from end to start)
        if (Physics.Raycast(endPos with { y = highestY }, -startToEndHorizontalDir.normalized, startToEndHorizontalDir.magnitude, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
            #if DEBUG
            linkVisualizers.Add(new LinkVisualizer(Color.red, false, endPos with { y = highestY }, startPos with { y = highestY }, 0.025f));
            ignoredHadColliderBetween++;
            #endif
            return;
        }

        // // if no walls 1 unit out then check for other colliders using the Cheat offset so as to not detect the edge we are spherecasting from.
        // var cheatStartPos = LerpByDistance(startPos, endPos, cheatOffset);
        // if (!Physics.SphereCast(cheatStartPos, sphereCastRadius, castDirection, out var raycastHit, maxJumpDist, raycastLayerMask.value, QueryTriggerInteraction.Ignore)) return;
        // // if (Physics.Linecast(startPos, endPos, out raycastHit, raycastLayerMask.value, QueryTriggerInteraction.Ignore))
        //
        // var cheatRaycastHit = LerpByDistance(raycastHit.point, endPos, .2f);
        //
        // if (!NavMesh.SamplePosition(cheatRaycastHit, out var navMeshHit, 1f,
        //         new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) return;
        // // Debug.DrawLine( pos, navMeshHit.position, Color.black, 15 );

        // Look for a navmesh
        if (!NavMesh.SamplePosition(raycastHit.point, out var navMeshHit, 1f, new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) return;

        // Todo: Figure out what this is
        // if ((Vector3.Distance(pos, navMeshHit.position) < 1.1f)) return;
        // if (Mathf.Abs(Vector3.Distance(pos, navMeshHit.position)) <= agent.Settings.agentClimb) return;

        // var startingPos = pos - normal * Vector3.forward * 0.02f;

        // Send the nav mesh link to the bake
        results.Add(new NavMeshLinkData() {
            startPosition = pos,
            endPosition = navMeshHit.position,
            width = agent.Settings.agentRadius,
            costModifier = 10f,
            bidirectional = true,
            area = 2,
            agentTypeID = agent.AgentTypeID,
        });


        #if DEBUG
        linkVisualizers.Add(new LinkVisualizer(Color.cyan, true, pos, navMeshHit.position));
        #endif
    }

    #endregion

    //Just a helper function I added to calculate a point between normalized distance of two V3s
    private static Vector3 LerpByDistance(Vector3 a, Vector3 b, float x) {
        return x * Vector3.Normalize(b - a) + a;
    }


    #region EdgeGen

    private const float TriggerAngle = 0.999f;

    private List<Edge> CalcEdges(Mesh currentMesh) {
        var edges = new List<Edge>();

        for (var i = 0; i < currentMesh.triangles.Length - 1; i += 3) {
            //CALC FROM MESH OPEN EDGES vertices

            TrisToEdge(edges, currentMesh, i, i + 1);
            TrisToEdge(edges, currentMesh, i + 1, i + 2);
            TrisToEdge(edges, currentMesh, i + 2, i);
        }

        foreach (var edge in edges) {
            //EDGE LENGTH
            edge.Length = Vector3.Distance(
                edge.Start,
                edge.End
            );

            //FACING NORMAL
            if (!edge.FacingNormalCalculated) {
                edge.FacingNormal = Quaternion.LookRotation(Vector3.Cross(edge.End - edge.Start, Vector3.up));

                if (edge.StartUp.sqrMagnitude > 0) {
                    var vect = Vector3.Lerp(edge.EndUp, edge.StartUp, 0.5f) - Vector3.Lerp(edge.End, edge.Start, 0.5f);
                    edge.FacingNormal = Quaternion.LookRotation(Vector3.Cross(edge.End - edge.Start, vect));

                    //FIX FOR NORMALs POINTING DIRECT TO UP/DOWN
                    if (Mathf.Abs(Vector3.Dot(Vector3.up, (edge.FacingNormal * Vector3.forward).normalized)) > TriggerAngle) {
                        edge.StartUp += new Vector3(0, 0.1f, 0);
                        vect = Vector3.Lerp(edge.EndUp, edge.StartUp, 0.5f) - Vector3.Lerp(edge.End, edge.Start, 0.5f);
                        edge.FacingNormal = Quaternion.LookRotation(Vector3.Cross(edge.End - edge.Start, vect));
                    }
                }

                if (dontAlignYAxis) {
                    edge.FacingNormal = Quaternion.LookRotation(
                        edge.FacingNormal * Vector3.forward,
                        Quaternion.LookRotation(edge.End - edge.Start) * Vector3.up
                    );
                }

                edge.FacingNormalCalculated = true;
            }

            if (invertFacingNormal) edge.FacingNormal = Quaternion.Euler(Vector3.up * 180) * edge.FacingNormal;
        }

        return edges;
    }

    private static void TrisToEdge(List<Edge> edges, Mesh currMesh, int n1, int n2) {
        var val1 = currMesh.vertices[currMesh.triangles[n1]];
        var val2 = currMesh.vertices[currMesh.triangles[n2]];

        var newEdge = new Edge(val1, val2);

        //remove duplicate edges
        foreach (var edge in edges) {
            if ((edge.Start == val1 & edge.End == val2) || (edge.Start == val2 & edge.End == val1)) {
                //print("Edges duplicate " + newEdge.start + " " + newEdge.end);
                edges.Remove(edge);
                return;
            }
        }

        edges.Add(newEdge);
    }

    #endregion
}

public class Edge {
    public Vector3 Start;
    public Vector3 End;

    public Vector3 StartUp;
    public Vector3 EndUp;

    public float Length;
    public Quaternion FacingNormal;
    public bool FacingNormalCalculated = false;


    public Edge(Vector3 startPoint, Vector3 endPoint) {
        Start = startPoint;
        End = endPoint;
    }
}

public class LinkVisualizer {

    private readonly Color _color;
    private readonly bool _constantWidth;
    private readonly Vector3 _startPos;
    private readonly Vector3 _endPos;
    private readonly float _width;

    internal LinkVisualizer(Color color, bool constantWidth, Vector3 startPos, Vector3 endPos, float width = 0.1f) {
        _color = color;
        _constantWidth = constantWidth;
        _startPos = startPos;
        _endPos = endPos;
        _width = width;
    }

    internal GameObject Instantiate() {

        var vis = new GameObject("vis");
        vis.transform.SetParent(NavMeshTools.NavMeshLinkGenerator.transform, false);
        var lineRenderer = vis.AddComponent<LineRenderer>();

        // Set line color
        var material = lineRenderer.material;
        material.shader = Shader.Find("Unlit/Color");
        material.color = _color;

        // Set width (you can modify this as needed)
        lineRenderer.startWidth = _width;
        lineRenderer.endWidth = _constantWidth ? _width : _width/10;

        // Set positions
        lineRenderer.positionCount = 2;  // We are defining a line with 2 points.
        lineRenderer.SetPosition(0, _startPos);
        lineRenderer.SetPosition(1, _endPos);

        return vis;
    }
}
