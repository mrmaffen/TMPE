namespace TrafficManager.Util {
    using System.Collections.Generic;
    using ColossalFramework;
    using API.Manager;
    using API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using API.Traffic.Enums;
    using System;
    using GenericGameBridge.Service;
    using UnityEngine;
    using CSUtil.Commons;
    using static TrafficManager.Util.SegmentTraverser;

    public static class PriorityRoad {
        public static void FixRoad(ushort initialSegmentId) {
            SegmentTraverser.Traverse(
                initialSegmentId,
                TraverseDirection.AnyDirection,
                TraverseSide.Straight,
                SegmentStopCriterion.None,
                VisitorFunc);
        }

        private static bool VisitorFunc(SegmentVisitData data) {
            ushort segmentId = data.CurSeg.segmentId;
            foreach (bool startNode in Constants.ALL_BOOL) {
                ushort nodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(segmentId, startNode);
                //ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
                FixJunction(nodeId);
            }
            return true;
        }

        public static void FixJunction(ushort nodeId) {
            if (nodeId == 0) {
                return;
            }
            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];

            // a list of segments attached to node arranged by size
            List<ushort> seglist = new List<ushort>();
            for (int i = 0; i < 8; ++i) {
                ushort segId = node.GetSegment(i);
                if (segId != 0) {
                    seglist.Add(segId);
                }
            }
            if (seglist.Count < 3) {
                // this is not a junctiuon
                return;
            }
            seglist.Sort(CompareSegments);

            if (CompareSegments(seglist[1], seglist[2]) == 0) {
                // all roads connected to the junction are equal.
                return;
            }

            // turning allowed when main road is oneway.
            bool ignoreLanes =
                ExtSegmentManager.Instance.CalculateIsOneWay(seglist[0]) ||
                ExtSegmentManager.Instance.CalculateIsOneWay(seglist[1]);

            // Turning allowed when the main road is agnled.
            ArrowDirection dir = GetDirection(seglist[0], seglist[1], nodeId);
            ignoreLanes &= dir != ArrowDirection.Forward;

            Debug.Log($"ignorelanes={ignoreLanes}");

            for (int i = 0; i < seglist.Count; ++i) {
                if (i < 2) {
                    FixMajorSegmentRules(seglist[i], nodeId);
                    if(!ignoreLanes) {
                        FixMajorSegmentLanes(seglist[i], nodeId);
                    }
                } else {
                    FixMinorSegmentRules(seglist[i], nodeId);
                    if (!ignoreLanes) {
                        FixMinorSegmentLanes(seglist[i], nodeId, ref seglist);
                    }
                }
            } //end for
        } // end method

        private static ArrowDirection GetDirection(ushort segmentId, ushort otherSegmentId, ushort nodeId) {
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
            ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];
            ArrowDirection dir = segEndMan.GetDirection(ref segEnd, otherSegmentId);
            return dir;
        }

        private static void FixMajorSegmentRules(ushort segmentId, ushort nodeId) {
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
            JunctionRestrictionsManager.Instance.SetEnteringBlockedJunctionAllowed(segmentId, startNode, true);
            JunctionRestrictionsManager.Instance.SetPedestrianCrossingAllowed(segmentId, startNode, false);
            TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Main);
        }

        private static void FixMinorSegmentRules(ushort segmentId, ushort nodeId) {
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);
            TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, PriorityType.Yield);
        }

        private static void FixMajorSegmentLanes(ushort segmentId, ushort nodeId) {
            if (LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) != SetLaneArrowError.Success) {
                Debug.Log("cant change lanes");
                return;
            }

            ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);



            //list of outgoing lanes from current segment to current node.
            IList<LanePos> laneList =
                Constants.ServiceFactory.NetService.GetSortedLanes(
                    segmentId,
                    ref seg,
                    startNode,
                    LaneArrowManager.LANE_TYPES,
                    LaneArrowManager.VEHICLE_TYPES,
                    true
                    );
            int srcLaneCount = laneList.Count;

            bool bLeft, bRight, bForward;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);


            //TODO: code for left hand drive
            //TODO: code for bendy avenue.
            // ban left turns and use of FR arrow where applicable.
            for (int i = 0; i < srcLaneCount; ++i) {
                LaneArrowManager.Instance.SetLaneArrows(
                    laneList[i].laneId,
                    LaneArrows.Forward);
            }
            if (srcLaneCount > 0 && bRight) {
                LanePos righMostLane = laneList[laneList.Count - 1];
                LaneArrowManager.Instance.SetLaneArrows(righMostLane.laneId, LaneArrows.ForwardRight);
            }
        }

        private static void FixMinorSegmentLanes(ushort segmentId, ushort nodeId, ref List<ushort> segList) {
            if (LaneArrowManager.SeparateTurningLanes.CanChangeLanes(segmentId, nodeId) != SetLaneArrowError.Success) {
                Debug.Log("can't change lanes");
                return;
            }
            ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
            bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, nodeId);

            //list of outgoing lanes from current segment to current node.
            IList<LanePos> laneList =
                Constants.ServiceFactory.NetService.GetSortedLanes(
                    segmentId,
                    ref seg,
                    startNode,
                    LaneArrowManager.LANE_TYPES,
                    LaneArrowManager.VEHICLE_TYPES,
                    true
                    );
            int srcLaneCount = laneList.Count;

            LaneArrows turnarrow = LaneArrows.Right;
            {
                // Check for slight right turn into the main road.
                IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
                ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
                ArrowDirection dir0 = segEndMan.GetDirection(ref segEnd, segList[0]);
                ArrowDirection dir1 = segEndMan.GetDirection(ref segEnd, segList[1]);
                Debug.Assert(dir1 != dir0); // Assume main road is not angled: then dir1 != dir0
                if (dir0 != ArrowDirection.Right && dir1 != ArrowDirection.Right) {
                    turnarrow = LaneArrows.Forward; //slight right uses forward arrow.
                }
            }

            // TODO: add code for bendy roads
            // TODO: add code for LHD
            // only right turn
            for (int i = 0; i < srcLaneCount; ++i) {
                LaneArrowManager.Instance.SetLaneArrows(
                    laneList[i].laneId,
                    turnarrow);
            }
        }

        private static int CompareSegments(ushort seg1Id, ushort seg2Id) {
            NetSegment seg1 = Singleton<NetManager>.instance.m_segments.m_buffer[seg1Id];
            NetSegment seg2 = Singleton<NetManager>.instance.m_segments.m_buffer[seg2Id];
            int diff = (int)Math.Ceiling(seg2.Info.m_halfWidth - seg1.Info.m_halfWidth);
            if (diff == 0) {
                diff = CountCarLanes(seg2Id) - CountCarLanes(seg1Id);
            }
            return diff;
        }

        private static int CountCarLanes(ushort segmentId) {
            NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            int forward = 0, backward = 0;
            segment.CountLanes(
                segmentId,
                        NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                        VehicleInfo.VehicleType.Car,
                        ref forward,
                        ref backward);
            return forward + backward;
        }
    } //end class
}
