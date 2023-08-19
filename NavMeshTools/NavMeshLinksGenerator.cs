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

    public float maxJumpHeight = 2.1f;
    public float maxJumpDist = 3f;
    private readonly LayerMask _raycastLayerMask = GetLayerMask();

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


    public (HashSet<NavMeshLinkData> navMeshLinkResults, HashSet<LinkVisualizer> navMeshLinkVisualizers) Generate(API.Agent agent, (Vector3[] vertices, int[] triangles) weldedNavMesh) {

        #if DEBUG
            ignoredNotAgentNavMesh = 0;
            ignoredHadColliderBetween = 0;
        #endif

        var navMeshLinksData = new HashSet<NavMeshLinkData>();
        var linkVisualizers = new HashSet<LinkVisualizer>();

        #if DEBUG
        MelonLogger.Msg("[Thread] Generating Mesh Links...");
        MelonLogger.Msg("\tCalculating Edges...");
        #endif

        // var edges = CalcEdges(navMeshTriangulation);
        var edges = MeshBoundaryFinder.FindBoundaryEdges(weldedNavMesh);
        #if DEBUG
        MelonLogger.Msg($"Found: {edges.Count} Edges!");
        #endif
        // foreach (var edge in edges) {
        //     var centerPosition = (edge.EndPos + edge.StartPos) * 0.5f;
        //     linkVisualizers.Add(new LinkVisualizer(Color.yellow, false, centerPosition, centerPosition + edge.Normal * 0.1f, 0.03f));
        //     linkVisualizers.Add(new LinkVisualizer(Color.green, true, centerPosition, centerPosition + Vector3.up, 0.015f));
        // }

        #if DEBUG
        MelonLogger.Msg("\tPlacing Tiles");
        #endif

        var edgeDivisionWidth = agent.Settings.agentRadius * 2;

        PlaceTiles(navMeshLinksData, linkVisualizers, agent, edgeDivisionWidth, edges);

        #if DEBUG
        MelonLogger.Msg($"\tDone! ignoredNotAgentNavMesh: {ignoredNotAgentNavMesh}, ignoredHadColliderBetween: {ignoredHadColliderBetween}");
        #endif

        return (navMeshLinksData, linkVisualizers);
    }

    private void PlaceTiles(HashSet<NavMeshLinkData> results, HashSet<LinkVisualizer> linkVisualizers, API.Agent agent, float edgeDivisionWidth, List<MeshBoundaryFinder.Edge> edges) {

        var raycastBuffer = new RaycastHit[20];
        var samplePoints = new List<(Vector3 placePos, Vector3 edgeNormal)>();

        foreach (var edge in edges) {

            // Always place at least 1 point on each edge
            var tilesCountWidth = (int)Mathf.Clamp(edge.Length / edgeDivisionWidth, 1, 10000);

            // Iterate over the segments of the edge
            for (var columnN = 0; columnN < tilesCountWidth; columnN++) {

                // Every edge length segment
                var placePos = Vector3.Lerp(
                    edge.StartPos,
                    edge.EndPos,
                    columnN / (float)tilesCountWidth
                    + 0.5f / tilesCountWidth
                );

                // Check if we're on the navmesh of our agent
                if (!NavMesh.SamplePosition(placePos, out _, 0.1f, new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) {
                    #if DEBUG
                    ignoredNotAgentNavMesh++;
                    linkVisualizers.Add(new LinkVisualizer(Color.red, true, placePos, placePos + Vector3.up * 0.5f, 0.02f));
                    #endif
                    continue;
                }

                samplePoints.Add((placePos, edge.Normal));
            }
        }

        foreach (var samplePoint in samplePoints) {

            #if DEBUG
            linkVisualizers.Add(new LinkVisualizer(Color.yellow, false, samplePoint.placePos, samplePoint.placePos + samplePoint.edgeNormal * 0.15f, 0.03f));
            linkVisualizers.Add(new LinkVisualizer(Color.yellow, true, samplePoint.placePos, samplePoint.placePos + Vector3.up, 0.005f));
            #endif

            // Spawn up/down links
            CheckPlacePos(results, linkVisualizers, samplePoint.placePos, samplePoint.edgeNormal, agent);
            // // Spawn horizontal links Todo: Fix the algo
            // CheckPlacePosHorizontal(results, linkVisualizers, samplePoint.placePos, samplePoint.edgeNormal, agent, raycastBuffer);
            // Check the sampled points against each other
            // CheckPlaceSampledPoints(results, linkVisualizers, samplePoint.placePos, samplePoint.edgeNormal, agent, raycastBuffer, samplePoints);
        }
    }


    private void CheckPlacePos(HashSet<NavMeshLinkData> results, HashSet<LinkVisualizer> linkVisualizers, Vector3 pos, Vector3 normalDirection, API.Agent agent) {

        var startPos = pos + normalDirection * agent.Settings.agentRadius * 2;

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
        var calcV3 = (pos - normalDirection * 0.02f);

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
        linkVisualizers.Add(new LinkVisualizer(Color.green, true, calcV3, navMeshHit.position, 0.01f));
        #endif
    }

    private void CheckPlacePosHorizontal(HashSet<NavMeshLinkData> results, HashSet<LinkVisualizer> linkVisualizers, Vector3 pos, Vector3 normalDirection, API.Agent agent, RaycastHit[] rayBuffer) {

        // var startPos = pos + normal * Vector3.forward * agent.Settings.agentRadius * 2;

        // Start a bit back across normals
        // var startPos = pos + normal * Vector3.back * agent.Settings.agentRadius;
        // And higher
        // startPos += Vector3.up * agent.Settings.agentHeight / 2;


        var sphereRadius = agent.Settings.agentHeight;

        // Start a bit in front of the normals, using the capsule radius
        // var startPos = pos + normal * Vector3.forward * capsuleRadius * 1.1f;
        var startPos = pos;
        // Start a bit ahead from the radius of the sphere cast, to avoid the initial edge
        var startPosSphereOffset = pos + normalDirection * sphereRadius * 1.1f;

        // Get a point a bit behind of the starting point and at half height of the agent
        var startPosWithOffset = startPos + Vector3.up * agent.Settings.agentHeight / 2f;

        // var endPos = startPos - normal * Vector3.back * maxJumpDist *
        // End at start across normals for the jumping distance
        var jumpDistance = maxJumpDist * 1.1f;
        var endPos = startPos + normalDirection * jumpDistance;

        var castDirection = endPos - startPos;

        // Do a sphere cast and grab all hits
        var rayCount = Physics.SphereCastNonAlloc(startPosSphereOffset, sphereRadius, castDirection, rayBuffer, jumpDistance, _raycastLayerMask.value, QueryTriggerInteraction.Ignore);

        // Ignore if there are no hits other than itself
        if (rayCount == 0 || rayCount == 1 && rayBuffer[0].distance == 0) {
            linkVisualizers.Add(new LinkVisualizer(Color.red, false, startPos, endPos, 0.01f));
            return;
        }

        for (var i = 0; i < rayCount; i++) {
            var raycastHit = rayBuffer[i];
            if (raycastHit.distance == 0) {
                linkVisualizers.Add(new LinkVisualizer(new Color(1, 0.3f, 0), false, startPos, startPos + Vector3.up, 0.035f));
                continue;
            }

            // Check if the hit is behind the starting point
            if (Vector3.Dot(castDirection.normalized, (raycastHit.point - startPos).normalized) < 0) {
                // linkVisualizers.Add(new LinkVisualizer(Color.red, false, startPos, endPos, 0.35f));
                linkVisualizers.Add(new LinkVisualizer(new Color(1, 0.5f, 0), false, startPos, startPos + Vector3.up, 0.35f));
                continue;
            }

            if (!NavMesh.SamplePosition(raycastHit.point, out var navMeshHit, agent.Settings.agentHeight, new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) {
                linkVisualizers.Add(new LinkVisualizer(Color.magenta, true, startPos, endPos, 0.02f));
                continue;
            }

            // Get a point a bit ahead of the ending point and at half height of the agent
            var endPosWithOffset = navMeshHit.position + Vector3.up * agent.Settings.agentHeight / 2f;

            // MelonLogger.Msg("[" + rayCount +"] " + raycastHit.point.ToString("F2") + " -> " + raycastHit.distance);
            // linkVisualizers.Add(new LinkVisualizer(Color.Lerp(Color.green, Color.magenta, i/(float)rayCount), false, startPos, raycastHit.point, capsuleRadius));


            // Check if there's a collider between the points at the highest height (from start to end)
            var startToEndHorizontalDir = endPosWithOffset - startPosWithOffset;
            var distanceStartToEnd = Vector3.Distance(startPosWithOffset, endPosWithOffset);
            var sphereCastRadiusHeight = agent.Settings.agentHeight / 2f * 0.9f;
            if (Physics.SphereCast(startPosWithOffset, sphereCastRadiusHeight, startToEndHorizontalDir, out _, distanceStartToEnd, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
                #if DEBUG
                linkVisualizers.Add(new LinkVisualizer(Color.blue, false, startPosWithOffset, endPosWithOffset, 0.02f));
                ignoredHadColliderBetween++;
                #endif
                continue;
            }

            // Check if there's a collider between the points at the highest height (from end to start)
            if (Physics.SphereCast(endPosWithOffset, sphereCastRadiusHeight, -startToEndHorizontalDir, out _, distanceStartToEnd, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
                #if DEBUG
                linkVisualizers.Add(new LinkVisualizer(Color.blue, false, endPosWithOffset, startPosWithOffset, 0.02f));
                ignoredHadColliderBetween++;
                #endif
                continue;
            }

            #if DEBUG
            linkVisualizers.Add(new LinkVisualizer(Color.yellow, true, startPosWithOffset, endPosWithOffset, 0.01f));
            linkVisualizers.Add(new LinkVisualizer(Color.green, false, startPos, navMeshHit.position, 0.01f));
            #endif

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
        }

        return;

        // // Cheat forward a little bit so the sphereCast doesn't touch this ledge.
        // // var cheatStartPos = LerpByDistance(startPos, endPos, cheatOffset);
        // //Debug.DrawRay(endPos, Vector3.up, Color.blue, 2);
        // //Debug.DrawLine ( cheatStartPos , endPos, Color.white, 2 );
        // //Debug.DrawLine(startPos, endPos, Color.white, 2);
        //
        // // Get the highest Y and add our agent's height to it, so see if it would be able to jump it
        // var highestY = (startPos.y > endPos.y ? startPos : endPos).y + agent.Settings.agentHeight;
        // var startToEndHorizontalDir = startPos with { y = highestY } - endPos with { y = highestY };
        //
        // // // Raise up pos Y value slightly up to check for wall/obstacle
        // // var offSetPosY = pos with { y = pos.y + wallCheckYOffset };
        // //
        // // // Ray cast to check for walls
        // // // if (Physics.Raycast(offSetPosY, sphereCastDirection, (maxJumpDist / 2), raycastLayerMask.value)) return;
        // // if (Physics.Raycast(offSetPosY, castDirection, (maxJumpDist / 2), raycastLayerMask.value)) return;
        // //
        // // var reverseRayCastSpot = offSetPosY + castDirection;
        // //
        // // //now raycast back the other way to make sure we're not raycasting through the inside of a mesh the first time.
        // // if (Physics.Raycast(reverseRayCastSpot, -castDirection, (maxJumpDist + 1), raycastLayerMask.value)) return;
        // // //Debug.DrawRay(ReverseRayCastSpot, -ReUsableV3, Color.red, 15);
        // // //Debug.DrawRay(ReverseRayCastSpot, -ReUsableV3, Color.red, 15);
        //
        //
        // // Calculate direction for Physics cast
        // // var castDirection = endPos - startPos;
        // // var capsuleRadius = agent.Settings.agentRadius * 2 - 0.01f;
        //
        // // // Look for a collider to jump to
        // // if (!Physics.CapsuleCast(startPos, endPos, capsuleRadius, castDirection, out var raycastHit, castDirection.magnitude, _raycastLayerMask, QueryTriggerInteraction.Ignore)) {
        // //     #if DEBUG
        // //     linkVisualizers.Add(new LinkVisualizer(Color.magenta, true, startPos, endPos with { y = highestY }, 0.02f));
        // //     #endif
        // //     // Todo: If this fails try a sphere cast?
        // //     return;
        // // }
        //
        // // if no walls 1 unit out then check for other colliders using the Cheat offset so as to not detect the edge we are spherecasting from.
        // var cheatStartPos = LerpByDistance(startPos, endPos, CheatOffset);
        // if (!Physics.SphereCast(cheatStartPos, sphereCastRadius, castDirection, out var raycastHit, maxJumpDist, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
        //     #if DEBUG
        //     linkVisualizers.Add(new LinkVisualizer(Color.magenta, true, startPos, endPos, 0.02f));
        //     #endif
        //     return;
        // }
        // // if (Physics.Linecast(startPos, endPos, out raycastHit, raycastLayerMask.value, QueryTriggerInteraction.Ignore))
        //
        //
        // // Check if there's a collider between the points at the highest height (from start to end)
        // if (!Physics.Raycast(startPos with { y = highestY }, startToEndHorizontalDir.normalized, startToEndHorizontalDir.magnitude, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
        //     #if DEBUG
        //     linkVisualizers.Add(new LinkVisualizer(Color.red, false, startPos with { y = highestY }, endPos with { y = highestY }, 0.025f));
        //     ignoredHadColliderBetween++;
        //     #endif
        //     return;
        // }
        // // Check if there's a collider between the points at the highest height (from end to start)
        // if (!Physics.Raycast(endPos with { y = highestY }, -startToEndHorizontalDir.normalized, startToEndHorizontalDir.magnitude, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
        //     #if DEBUG
        //     linkVisualizers.Add(new LinkVisualizer(Color.red, false, endPos with { y = highestY }, startPos with { y = highestY }, 0.025f));
        //     ignoredHadColliderBetween++;
        //     #endif
        //     return;
        // }
        //
        // // // if no walls 1 unit out then check for other colliders using the Cheat offset so as to not detect the edge we are spherecasting from.
        // // var cheatStartPos = LerpByDistance(startPos, endPos, cheatOffset);
        // // if (!Physics.SphereCast(cheatStartPos, sphereCastRadius, castDirection, out var raycastHit, maxJumpDist, raycastLayerMask.value, QueryTriggerInteraction.Ignore)) return;
        // // // if (Physics.Linecast(startPos, endPos, out raycastHit, raycastLayerMask.value, QueryTriggerInteraction.Ignore))
        // //
        // // var cheatRaycastHit = LerpByDistance(raycastHit.point, endPos, .2f);
        // //
        // // if (!NavMesh.SamplePosition(cheatRaycastHit, out var navMeshHit, 1f,
        // //         new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) return;
        // // // Debug.DrawLine( pos, navMeshHit.position, Color.black, 15 );
        //
        // // Look for a navmesh
        // if (!NavMesh.SamplePosition(raycastHit.point, out var navMeshHit, 1f, new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.AgentTypeID })) return;
        //
        // // Todo: Figure out what this is
        // // if ((Vector3.Distance(pos, navMeshHit.position) < 1.1f)) return;
        // // if (Mathf.Abs(Vector3.Distance(pos, navMeshHit.position)) <= agent.Settings.agentClimb) return;
        //
        // // var startingPos = pos - normal * Vector3.forward * 0.02f;
        //
        // // Send the nav mesh link to the bake
        // results.Add(new NavMeshLinkData() {
        //     startPosition = pos,
        //     endPosition = navMeshHit.position,
        //     width = agent.Settings.agentRadius,
        //     costModifier = 10f,
        //     bidirectional = true,
        //     area = 2,
        //     agentTypeID = agent.AgentTypeID,
        // });
        //
        //
        // #if DEBUG
        // linkVisualizers.Add(new LinkVisualizer(Color.cyan, true, pos, navMeshHit.position, 0.025f));
        // #endif
    }

    private void CheckPlaceSampledPoints(HashSet<NavMeshLinkData> results, HashSet<LinkVisualizer> linkVisualizers,
        Vector3 pos, Vector3 normalDirection, API.Agent agent, RaycastHit[] rayBuffer,
        List<(Vector3 placePos, Vector3 edgeNormal)> samplePoints) {

        var sphereCastRadiusHeight = agent.Settings.agentHeight / 2f * 0.75f;

        var startHeightOffset = pos + Vector3.up * agent.Settings.agentHeight / 2f;
        var startHeightAndNormalOffset = startHeightOffset - normalDirection * agent.Settings.agentHeight / 2f;

        var targets = new Dictionary<NavMeshLinkData, float>();

        // Check all other points within the max distances
        foreach (var possibleTarget in samplePoints) {

            // Remove targets by distance
            var difference = possibleTarget.placePos - pos;
            var verticalDistance = Mathf.Abs(difference.y);
            if (verticalDistance > maxJumpHeight) {
                #if DEBUG
                // linkVisualizers.Add(new LinkVisualizer(new Color (1, 0, 0.5f), true, pos, possibleTarget.placePos, 0.01f));
                #endif
                continue;
            }
            var horizontalDistance = Mathf.Sqrt(difference.x * difference.x + difference.z * difference.z);
            if (horizontalDistance > maxJumpDist) {
                #if DEBUG
                // linkVisualizers.Add(new LinkVisualizer(new Color (.5f, 0, 1f), true, pos, possibleTarget.placePos, 0.01f));
                #endif
                continue;
            }

            // Get the direction from pos to possible target
            var directionToTarget = difference.normalized;

            // If the target is not withing 45 degrees, lets skip!
            if (Vector3.Angle(directionToTarget, normalDirection) > 45f) {
                #if DEBUG
                // linkVisualizers.Add(new LinkVisualizer(new Color (1f, .5f, 0f), true, pos, possibleTarget.placePos, 0.01f));
                #endif
                continue;
            }

            var endHeightOffset = possibleTarget.placePos + Vector3.up * agent.Settings.agentHeight / 2f;
            var endHeightAndNormalOffset = endHeightOffset - possibleTarget.edgeNormal * agent.Settings.agentHeight / 2f;

            // Linecast a bit back across normals to ensure we're not in a collider (start -> end)
            if (Physics.Linecast(startHeightAndNormalOffset, endHeightAndNormalOffset, out _, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
                #if DEBUG
                // linkVisualizers.Add(new LinkVisualizer(Color.blue, false, pos, possibleTarget.placePos, 0.02f));
                ignoredHadColliderBetween++;
                #endif
                continue;
            }

            // Linecast a bit back across normals to ensure we're not in a collider (start -> end)
            if (Physics.Linecast(endHeightAndNormalOffset, startHeightAndNormalOffset, out _, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
                #if DEBUG
                // linkVisualizers.Add(new LinkVisualizer(Color.blue, false, possibleTarget.placePos, pos, 0.02f));
                ignoredHadColliderBetween++;
                #endif
                continue;
            }

            var startToEndHorizontalDir = endHeightOffset - startHeightOffset;
            var distanceStartToEnd = Vector3.Distance(endHeightOffset, startHeightOffset);

            // Sphere cast at half height of the agent with a sphere with radius half of the agent (start -> end)
            if (Physics.SphereCast(startHeightOffset, sphereCastRadiusHeight, startToEndHorizontalDir, out _, distanceStartToEnd, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
                #if DEBUG
                // linkVisualizers.Add(new LinkVisualizer(Color.blue, false, pos, possibleTarget.placePos, 0.02f));
                ignoredHadColliderBetween++;
                #endif
                continue;
            }

            // Sphere cast at half height of the agent with a sphere with radius half of the agent (end -> start)
            if (Physics.SphereCast(endHeightOffset, sphereCastRadiusHeight, -startToEndHorizontalDir, out _, distanceStartToEnd, _raycastLayerMask.value, QueryTriggerInteraction.Ignore)) {
                #if DEBUG
                // linkVisualizers.Add(new LinkVisualizer(Color.blue, false, possibleTarget.placePos, pos, 0.02f));
                ignoredHadColliderBetween++;
                #endif
                continue;
            }

            // #if DEBUG
            // linkVisualizers.Add(new LinkVisualizer(Color.green, false, pos, possibleTarget.placePos, 0.01f));
            // #endif

            // Add the possible targets into a dictionary along their distances so we can use the closest ones
            targets.Add(new NavMeshLinkData() {
                startPosition = pos,
                endPosition = possibleTarget.placePos,
                width = agent.Settings.agentRadius,
                costModifier = 10f,
                bidirectional = true,
                area = 2,
                agentTypeID = agent.AgentTypeID,
            }, verticalDistance + horizontalDistance);
        }


        // Pick 3 targets with the lowest distances
        var topThree = targets.OrderBy(kv => kv.Value).Take(3).ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var target in topThree) {
            #if DEBUG
            linkVisualizers.Add(new LinkVisualizer(Color.green, false, target.Key.startPosition, target.Key.endPosition, 0.01f));
            #endif
            // Send the nav mesh link to the bake
            results.Add(target.Key);
        }
    }
}

public static class MeshBoundaryFinder {

    public readonly struct Edge {

        private readonly int _v1;
        private readonly int _v2;
        public readonly float Length;
        public readonly Vector3 Normal;
        public readonly Vector3 StartPos;
        public readonly Vector3 EndPos;

        public Edge(int a, int b, float length, Vector3 normal, Vector3 startPos, Vector3 endPos) {
            _v1 = Mathf.Min(a, b);
            _v2 = Mathf.Max(a, b);
            Length = length;
            Normal = normal;
            StartPos = startPos;
            EndPos = endPos;
        }

        public override bool Equals(object obj) {
            if (obj == null) return false;
            var other = (Edge) obj;
            return _v1 == other._v1 && _v2 == other._v2;
        }

        public override int GetHashCode() => _v1.GetHashCode() ^ _v2.GetHashCode();
    }

    public static List<Edge> FindBoundaryEdges((Vector3[] vertices, int[] triangles) weldedNavMesh) {

        var triangles = weldedNavMesh.triangles;
        var vertices = weldedNavMesh.vertices;

        var edges = new Dictionary<Edge, int>();

        for (var i = 0; i < triangles.Length; i += 3) {
            var p0 = vertices[triangles[i]];
            var p1 = vertices[triangles[i + 1]];
            var p2 = vertices[triangles[i + 2]];

            // Calculate the face normal
            var faceNormal = Vector3.Cross(p1 - p0, p2 - p0).normalized;

            for (var j = 0; j < 3; j++) {
                var vertexIndex1 = triangles[i + j];
                var vertexIndex2 = triangles[i + (j + 1) % 3];
                var edgeStartPos = vertices[vertexIndex1];
                var edgeEndPos = vertices[vertexIndex2];
                var length = Vector3.Distance(edgeStartPos, edgeEndPos);

                // Calculate the normal of the edge from it's midpoint
                var midpoint = (edgeStartPos + edgeEndPos) * 0.5f;
                var outwardDirection = (midpoint - edgeStartPos).normalized;
                var edgeNormal = Vector3.Cross(outwardDirection, faceNormal).normalized;

                var edge = new Edge(vertexIndex1, vertexIndex2, length, edgeNormal, edgeStartPos, edgeEndPos);

                if (edges.ContainsKey(edge)) {
                    edges[edge]++;
                }
                else {
                    edges[edge] = 1;
                }
            }
        }

        // Get all edges that are shared by only one triangle (Means it's a boundary edge)
        return (from kvp in edges where kvp.Value == 1 select kvp.Key).ToList();
    }

    public static (Vector3[] vertices, int[] triangles) WeldVertices(Vector3[] originalVerts, int[] originalTriangles) {

        var vertexMap = new Dictionary<Vector3, int>();
        const int precisionDecimals = 3;

        var uniqueVertices = new List<Vector3>();
        // Create a dictionary of unique vertices
        for (var i = 0; i < originalVerts.Length; i++) {
            var vertex = originalVerts[i];
            var roundedVertex = new Vector3(
                Mathf.Round(vertex.x * precisionDecimals) / precisionDecimals,
                Mathf.Round(vertex.y * precisionDecimals) / precisionDecimals,
                Mathf.Round(vertex.z * precisionDecimals) / precisionDecimals
            );

            if (!vertexMap.ContainsKey(roundedVertex)) {
                vertexMap[roundedVertex] = uniqueVertices.Count;
                uniqueVertices.Add(vertex);
            }
        }

        // Replace the indices in the triangles list
        var triangles = new int[originalTriangles.Length];
        for (var i = 0; i < originalTriangles.Length; i++) {
            var vertex = originalVerts[originalTriangles[i]];
            var roundedVertex = new Vector3(
                Mathf.Round(vertex.x * precisionDecimals) / precisionDecimals,
                Mathf.Round(vertex.y * precisionDecimals) / precisionDecimals,
                Mathf.Round(vertex.z * precisionDecimals) / precisionDecimals
            );

            triangles[i] = vertexMap[roundedVertex];
        }
        #if DEBUG
        MelonLogger.Msg($"Finished mesh welding process with {precisionDecimals} decimals precision. Vertex Count: {originalVerts.Length} -> {uniqueVertices.Count}");
        #endif

        // Return the welded vertices and triangles
        return (uniqueVertices.ToArray(), triangles);
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
        vis.transform.SetParent(NavMeshTools.NavMeshToolsGo.transform, false);
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
