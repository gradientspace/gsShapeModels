// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using g3;

namespace gs
{
    public class ReprojectOp : BaseRemeshOp
    {
        public enum ReprojectModes
        {
            SmoothSurfaceFlow = 0,
            SharpEdgesFlow = 1,
            BoundedDistance = 2
        }
        ReprojectModes reproject_mode = ReprojectModes.SmoothSurfaceFlow;
        public virtual ReprojectModes ReprojectMode {
            get { return reproject_mode; }
            set {
                if (reproject_mode != value) { reproject_mode = value; invalidate(); }
            }
        }



        double target_edge_length = 1.0;
        public virtual double TargetEdgeLength {
            get { return target_edge_length; }
            set {
                double set_len = MathUtil.Clamp(value, 0.0001, 10000.0);
                if (Math.Abs(target_edge_length - set_len) > MathUtil.ZeroTolerancef) { target_edge_length = set_len; invalidate(); }
            }
        }


        int remesh_rounds = 20;
        public virtual int RemeshRounds {
            get { return remesh_rounds; }
            set {
                int set_value = MathUtil.Clamp(value, 1, 500);
                if (remesh_rounds != set_value) { remesh_rounds = set_value; invalidate(); }
            }
        }


        int project_rounds = 40;
        public virtual int ProjectionRounds {
            get { return project_rounds; }
            set {
                int set_value = MathUtil.Clamp(value, 1, 500);
                if (project_rounds != set_value) { project_rounds = set_value; invalidate(); }
            }
        }


        double max_project_distance = double.MaxValue;
        public virtual double TargetMaxDistance {
            get { return max_project_distance; }
            set {
                double set_size = MathUtil.Clamp(value, 0.0001, double.MaxValue);
                if (Math.Abs(max_project_distance - set_size) > MathUtil.ZeroTolerancef) { max_project_distance = value; invalidate(); }
            }
        }


        double transition_smoothness = 0.5;
        public virtual double TransitionSmoothness {
            get { return transition_smoothness; }
            set {
                double set_val = MathUtil.Clamp(value, 0, 1);
                if (Math.Abs(transition_smoothness - set_val) > MathUtil.ZeroTolerancef) { transition_smoothness = value; invalidate(); }
            }
        }




        DMeshSourceOp mesh_source;
        public DMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null)
                    mesh_source.OperatorModified -= on_input_modified;
                mesh_source = value;
                if (mesh_source != null)
                    mesh_source.OperatorModified += on_input_modified;
                invalidate();
            }
        }


        DMeshSourceOp target_source;
        public DMeshSourceOp TargetSource {
            get { return target_source; }
            set {
                if (target_source != null)
                    target_source.OperatorModified -= on_input_modified;
                target_source = value;
                if (target_source != null)
                    target_source.OperatorModified += on_input_modified;
                invalidate();
            }
        }


        TransformSequence source_to_target = new TransformSequence();
        TransformSequence target_to_source = new TransformSequence();
        public TransformSequence TransformToTarget {
            get { return source_to_target; }
            set {
                source_to_target = new TransformSequence(value);
                target_to_source = source_to_target.MakeInverse();
                invalidate();
            }
        }



        protected DMesh3 ResultMesh;
        protected override DMesh3 GetResultMesh(bool bConsume) {
            var mesh = ResultMesh;
            if (bConsume)
                ResultMesh = null;
            return mesh;
        }


        public override void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("ReprojectOp: must set valid MeshSource to compute!");

            try {
                if (ReprojectMode == ReprojectModes.SmoothSurfaceFlow)
                    ResultMesh = compute_smooth();
                else if (ReprojectMode == ReprojectModes.SharpEdgesFlow)
                    ResultMesh = compute_sharp_edge_flow();
                else
                    ResultMesh = compute_bounded_distance();

                base.complete_update();

            } catch (Exception e) {
                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
            }
        }







        protected virtual DMesh3 compute_smooth()
        {
            DMesh3 sourceMesh = MeshSource.GetDMeshUnsafe();
            ISpatial inputSpatial = MeshSource.GetSpatial();

            DMesh3 targetMesh = TargetSource.GetDMeshUnsafe();
            ISpatial targetSpatial = TargetSource.GetSpatial();

            DMesh3 meshIn = new DMesh3(sourceMesh);
            RemesherPro remesher = new RemesherPro(meshIn);

            remesher.SetTargetEdgeLength(TargetEdgeLength);
            remesher.PreventNormalFlips = this.PreventNormalFlips;
            remesher.EnableFlips = this.EnableFlips;
            remesher.EnableSplits = this.EnableSplits;
            remesher.EnableCollapses = this.EnableCollapses;
            remesher.EnableSmoothing = this.EnableSmoothing;
            remesher.SmoothSpeedT = this.SmoothingSpeed;

            BoundedProjectionTarget target = new BoundedProjectionTarget() {
                Source = sourceMesh, SourceSpatial = inputSpatial,
                Target = targetMesh, TargetSpatial = targetSpatial,
                SourceToTargetXForm = source_to_target,
                TargetToSourceXForm = target_to_source,
                MaxDistance = double.MaxValue
            };

            remesher.SetProjectionTarget(target);

            if (sourceMesh.CachedIsClosed == false) {
                if (remesher.Constraints == null)
                    remesher.SetExternalConstraints(new MeshConstraints());

                if (BoundaryMode == BoundaryModes.FreeBoundaries) {
                    MeshConstraintUtil.PreserveBoundaryLoops(remesher.Constraints, meshIn);
                } else if (BoundaryMode == BoundaryModes.FixedBoundaries) {
                    MeshConstraintUtil.FixAllBoundaryEdges(remesher.Constraints, meshIn);
                } else if (BoundaryMode == BoundaryModes.ConstrainedBoundaries) {
                    MeshConstraintUtil.FixAllBoundaryEdges_AllowSplit(remesher.Constraints, meshIn, 0);
                }
            }
            if (is_invalidated())
                return null;

            remesher.Progress = new ProgressCancel(is_invalidated);
            remesher.FastestRemesh(RemeshRounds);
            if (is_invalidated())
                return null;

            return meshIn;
        }











        protected virtual DMesh3 compute_bounded_distance()
        {
            DMesh3 sourceMesh = MeshSource.GetDMeshUnsafe();
            ISpatial inputSpatial = MeshSource.GetSpatial();

            DMesh3 targetMesh = TargetSource.GetDMeshUnsafe();
            ISpatial targetSpatial = TargetSource.GetSpatial();

            double max_dist = (TargetMaxDistance == double.MaxValue) ? double.MaxValue : TargetMaxDistance;

            DMesh3 meshIn = new DMesh3(sourceMesh);

            bool target_closed = targetMesh.IsClosed();
            MeshVertexSelection roiV = new MeshVertexSelection(meshIn);
            SpinLock roi_lock = new SpinLock();
            gParallel.ForEach(meshIn.VertexIndices(), (vid) => {
                Vector3d pos = meshIn.GetVertex(vid);
                Vector3d posTarget = TransformToTarget.TransformP(pos);
                double dist = MeshQueries.NearestPointDistance(targetMesh, targetSpatial, posTarget, max_dist);
                bool inside = (target_closed && targetSpatial.IsInside(posTarget));
                if ( dist < max_dist || inside ) {
                    bool taken = false;
                    roi_lock.Enter(ref taken);
                    roiV.Select(vid);
                    roi_lock.Exit();
                }
            });
            if (is_invalidated())
                return null;

            MeshFaceSelection roi_faces = new MeshFaceSelection(meshIn, roiV, 1);
            roi_faces.ExpandToOneRingNeighbours(3);
            roi_faces.LocalOptimize();
            if (is_invalidated())
                return null;

            RegionOperator op = new RegionOperator(meshIn, roi_faces);
            DMesh3 meshROI = op.Region.SubMesh;
            if (is_invalidated())
                return null;

            RemesherPro remesher = new RemesherPro(meshROI);

            remesher.SetTargetEdgeLength(TargetEdgeLength);
            remesher.PreventNormalFlips = this.PreventNormalFlips;
            remesher.EnableFlips = this.EnableFlips;
            remesher.EnableSplits = this.EnableSplits;
            remesher.EnableCollapses = this.EnableCollapses;
            remesher.EnableSmoothing = this.EnableSmoothing;
            remesher.SmoothSpeedT = this.SmoothingSpeed;

            BoundedProjectionTarget target = new BoundedProjectionTarget() {
                Source = sourceMesh, SourceSpatial = inputSpatial,
                Target = targetMesh, TargetSpatial = targetSpatial,
                SourceToTargetXForm = source_to_target,
                TargetToSourceXForm = target_to_source,
                MaxDistance = max_dist,
                Smoothness = transition_smoothness
            };

            remesher.SetProjectionTarget(target);

            if (remesher.Constraints == null)
                remesher.SetExternalConstraints(new MeshConstraints());
            MeshConstraintUtil.FixAllBoundaryEdges(remesher.Constraints, meshROI);
            if (is_invalidated())
                return null;

            remesher.Progress = new ProgressCancel(is_invalidated);
            remesher.FastestRemesh(RemeshRounds);
            if (is_invalidated())
                return null;

            op.BackPropropagate();

            return meshIn;
        }








        protected virtual DMesh3 compute_sharp_edge_flow()
        {
            DMesh3 sourceMesh = MeshSource.GetDMeshUnsafe();
            ISpatial inputSpatial = MeshSource.GetSpatial();

            DMesh3 targetMesh = TargetSource.GetDMeshUnsafe();
            ISpatial targetSpatial = TargetSource.GetSpatial();

            DMesh3 meshIn = new DMesh3(sourceMesh);
            if (is_invalidated())
                return null;

            RemesherPro remesher = new RemesherPro(meshIn);
            remesher.SetTargetEdgeLength(TargetEdgeLength);
            remesher.PreventNormalFlips = this.PreventNormalFlips;
            remesher.EnableFlips = this.EnableFlips;
            remesher.EnableSplits = this.EnableSplits;
            remesher.EnableCollapses = this.EnableCollapses;
            remesher.EnableSmoothing = this.EnableSmoothing;
            remesher.SmoothSpeedT = this.SmoothingSpeed;

            TransformedMeshProjectionTarget target = 
                new TransformedMeshProjectionTarget(targetMesh, targetSpatial) {
                    SourceToTargetXForm = source_to_target,
                    TargetToSourceXForm = target_to_source
                };
            remesher.SetProjectionTarget(target);

            if (sourceMesh.CachedIsClosed == false) {
                if (remesher.Constraints == null)
                    remesher.SetExternalConstraints(new MeshConstraints());

                if (BoundaryMode == BoundaryModes.FreeBoundaries) {
                    MeshConstraintUtil.PreserveBoundaryLoops(remesher.Constraints, meshIn);
                } else if (BoundaryMode == BoundaryModes.FixedBoundaries) {
                    MeshConstraintUtil.FixAllBoundaryEdges(remesher.Constraints, meshIn);
                } else if (BoundaryMode == BoundaryModes.ConstrainedBoundaries) {
                    MeshConstraintUtil.FixAllBoundaryEdges_AllowSplit(remesher.Constraints, meshIn, 0);
                }
            }
            if (is_invalidated())
                return null;

            remesher.Progress = new ProgressCancel(is_invalidated);
            remesher.SharpEdgeReprojectionRemesh(RemeshRounds, ProjectionRounds);

            if (is_invalidated())
                return null;
            return meshIn;
        }




        class BoundedProjectionTarget : IProjectionTarget
        {
            public DMesh3 Source { get; set; }
            public ISpatial SourceSpatial { get; set; }

            public DMesh3 Target { get; set; }
            public ISpatial TargetSpatial { get; set; }

            public TransformSequence SourceToTargetXForm;
            public TransformSequence TargetToSourceXForm;

            public double MaxDistance = double.MaxValue;
            public double Smoothness = 0.5;

            public Vector3d Project(Vector3d vSourcePt, int identifier = -1)
            {
                Vector3d vTargetPt = SourceToTargetXForm.TransformP(vSourcePt);

                int tNearestID = TargetSpatial.FindNearestTriangle(vTargetPt);
                DistPoint3Triangle3 q = MeshQueries.TriangleDistance(Target, tNearestID, vTargetPt);
                double d = q.DistanceSquared;
                Vector3d vTargetNearestInSource = TargetToSourceXForm.TransformP(q.TriangleClosest);
                if (MaxDistance == double.MaxValue)
                    return vTargetNearestInSource;

                if (TargetSpatial.IsInside(vTargetPt))
                    return vTargetNearestInSource;

                tNearestID = SourceSpatial.FindNearestTriangle(vSourcePt);
                DistPoint3Triangle3 qSource = MeshQueries.TriangleDistance(Source, tNearestID, vSourcePt);

                d = Math.Sqrt(d);
                if (d < MaxDistance) {
                    double min = (1.0-Smoothness)*MaxDistance;
                    double t = MathUtil.WyvillFalloff(d, min, MaxDistance);
                    t = 1.0-t;
                    return Vector3d.Lerp(vTargetNearestInSource, qSource.TriangleClosest, t);
                } else {
                    return qSource.TriangleClosest;
                }
            }
        }









    }


}
