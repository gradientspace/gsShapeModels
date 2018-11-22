// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{
    /// <summary>
    /// RemeshOp implements basic remeshing pass.
    /// 
    /// [TODO] for remeshed boundary mode, we are recomputing MeshBoundaryLoops each time...
    /// 
    /// </summary>
    public class RemeshOp : BaseRemeshOp
    {
        double target_edge_length = 1.0;
        public virtual double TargetEdgeLength {
            get { return target_edge_length; }
            set {
                double set_len = MathUtil.Clamp(value, 0.0001, 10000.0);
                if (Math.Abs(target_edge_length - set_len) > MathUtil.ZeroTolerancef) { target_edge_length = set_len; invalidate(); }
            }
        }


        bool reproject_to_input = true;
        public virtual bool ReprojectToInput {
            get { return reproject_to_input; }
            set { if (reproject_to_input != value) { reproject_to_input = value; invalidate(); } }
        }


        bool preserve_creases = false;
        public virtual bool PreserveCreases {
            get { return preserve_creases; }
            set { if (preserve_creases != value) { preserve_creases = value; invalidate(); } }
        }

        double crease_angle = 30.0;
        public virtual double CreaseAngle {
            get { return crease_angle; }
            set {
                double set_angle = MathUtil.Clamp(value, 1.0, 90.0);
                if (Math.Abs(crease_angle - set_angle) > MathUtil.ZeroTolerancef) { crease_angle = set_angle; invalidate(); }
            }
        }

        int max_rounds = 25;
        public virtual int RemeshRounds {
            get { return max_rounds; }
            set {
                int set_value = MathUtil.Clamp(value, 1, 500);
                if (max_rounds != set_value) { max_rounds = set_value; invalidate(); } }
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
                throw new Exception("RemeshOp: must set valid MeshSource to compute!");

            try {
                ResultMesh = compute_standard();
                base.complete_update();

            } catch (Exception e) {
                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
            }

        }

        protected virtual DMesh3 compute_standard()
        {
            DMesh3 sourceMesh = MeshSource.GetDMeshUnsafe();
            ISpatial sourceSpatial = MeshSource.GetSpatial();
            DMesh3 meshIn = new DMesh3(sourceMesh);

            RemesherPro remesh = new RemesherPro(meshIn);
            //Remesher remesh = new Remesher(meshIn);

            remesh.SetTargetEdgeLength(TargetEdgeLength);
            remesh.PreventNormalFlips = this.PreventNormalFlips;
            remesh.EnableFlips = this.EnableFlips;
            remesh.EnableSplits = this.EnableSplits;
            remesh.EnableCollapses = this.EnableCollapses;
            remesh.EnableSmoothing = this.EnableSmoothing;
            remesh.SmoothSpeedT = this.SmoothingSpeed;

            if ( ReprojectToInput ) {
                MeshProjectionTarget target = new MeshProjectionTarget(sourceMesh, sourceSpatial);
                remesh.SetProjectionTarget(target);
            }


            // if we are preserving creases, this will also automatically constrain boundary
            // edges boundary loops/spans. 
            if ( preserve_creases ) {
                if (remesh.Constraints == null)
                    remesh.SetExternalConstraints(new MeshConstraints());

                MeshTopology topo = new MeshTopology(meshIn);
                topo.CreaseAngle = this.CreaseAngle;
                topo.AddRemeshConstraints(remesh.Constraints);

                // replace boundary edge constraints if we want other behaviors
                if (BoundaryMode == BoundaryModes.FixedBoundaries)
                    MeshConstraintUtil.FixEdges(remesh.Constraints, meshIn, topo.BoundaryEdges);

            } else if (sourceMesh.CachedIsClosed == false) {
                if (remesh.Constraints == null)
                    remesh.SetExternalConstraints(new MeshConstraints());

                if (BoundaryMode == BoundaryModes.FreeBoundaries) {
                    MeshConstraintUtil.PreserveBoundaryLoops(remesh.Constraints, meshIn);
                } else if (BoundaryMode == BoundaryModes.FixedBoundaries) {
                    MeshConstraintUtil.FixAllBoundaryEdges(remesh.Constraints, meshIn);
                } else if (BoundaryMode == BoundaryModes.ConstrainedBoundaries) {
                    MeshConstraintUtil.FixAllBoundaryEdges_AllowSplit(remesh.Constraints, meshIn, 0);
                }
            }

            remesh.Progress = new ProgressCancel(is_invalidated);

            remesh.FastestRemesh(RemeshRounds, true);
            //for (int k = 0; k < RemeshRounds; ++k)
            //    remesh.BasicRemeshPass();

            // free boundary remesh can leave sliver triangles around the border. clean that up.
            if (sourceMesh.CachedIsClosed == false && BoundaryMode == BoundaryModes.FreeBoundaries ) {
                MeshEditor.RemoveFinTriangles(meshIn, (mesh, tid) => {
                    Index3i tv = mesh.GetTriangle(tid);
                    return MathUtil.AspectRatio(mesh.GetVertex(tv.a), mesh.GetVertex(tv.b), mesh.GetVertex(tv.c)) > 2;
                });
            }

            if (is_invalidated())
                return null;
            return meshIn;
        }



    }


}
