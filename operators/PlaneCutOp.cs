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
    public class PlaneCutOp : BaseDMeshSourceOp
    {
        bool fill_holes = true;
        public bool FillHoles {
            get { return fill_holes; }
            set {
                if (fill_holes != value) { fill_holes = value; invalidate(); }
            }
        }

        bool reverse_normal = false;
        public bool ReverseNormal {
            get { return reverse_normal; }
            set {
                if (reverse_normal != value) { reverse_normal = value; invalidate(); }
            }
        }

        bool return_both = false;
        public bool ReturnBothSides {
            get { return return_both; }
            set {
                if (return_both != value) { return_both = value; invalidate(); }
            }
        }

        double tolerance = 0.001;
        public double Tolerance {
            get { return tolerance; }
            set {
                if (Math.Abs(tolerance - value) > MathUtil.ZeroTolerance) { tolerance = value; invalidate(); }
            }
        }



        bool minimal_fill = false;
        public bool MinimalFill {
            get { return minimal_fill; }
            set {
                if (minimal_fill != value) { minimal_fill = value; invalidate(); }
            }
        }

        double target_fill_len = 1.0;
        public double FillEdgeLength {
            get { return target_fill_len; }
            set {
                if (Math.Abs(target_fill_len - value) > MathUtil.ZeroTolerance) { target_fill_len = value; invalidate(); }
            }
        }


        Vector3d plane_origin = Vector3f.Zero;
        public Vector3d PlaneOrigin {
            get { return plane_origin; }
            set {
                if (plane_origin.EpsilonEqual(value, MathUtil.ZeroTolerance) == false) { plane_origin = value; invalidate(); }
            }
        }

        Vector3d plane_normal = Vector3f.Zero;
        public Vector3d PlaneNormal {
            get { return plane_normal; }
            set {
                if (plane_normal.EpsilonEqual(value, MathUtil.ZeroTolerance) == false) { plane_normal = value.Normalized; invalidate(); }
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


        protected virtual void on_input_modified(ModelingOperator op)
        {
            base.invalidate();
        }


        DMesh3 ResultMesh;
        DMesh3 OtherSideResultMesh;


        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            Vector3d cache_normal = PlaneNormal;
            Vector3d cache_origin = PlaneOrigin;
            bool cache_fill_holes = FillHoles;
            double cache_tolerance = Tolerance;
            bool cache_min_fill = MinimalFill;
            double cache_fill_len = (MinimalFill) ? double.MaxValue : FillEdgeLength;
            bool cache_both = ReturnBothSides;
            if (reverse_normal)
                cache_normal = -cache_normal;

            if (MeshSource == null)
                throw new Exception("GenerateClosedMeshOp: must set valid MeshSource to compute!");

            int try_count = 0;
            int max_tries = (cache_min_fill) ? 0 : 3;
            try_again:

            ResultMesh = null;
            OtherSideResultMesh = null;

            try {
                bool fill_errors;
                ResultMesh = compute_cut(cache_origin, cache_normal, cache_tolerance, cache_fill_holes, cache_fill_len, out fill_errors);

                bool fill_errors_otherside = false;
                if (cache_both)
                    OtherSideResultMesh = compute_cut(cache_origin, -cache_normal, cache_tolerance, cache_fill_holes, cache_fill_len, out fill_errors_otherside);
                else
                    OtherSideResultMesh = null;

                if ( fill_errors || fill_errors_otherside) {
                    throw new Exception("fill_errors");
                    //ResultMesh = base.make_failure_output(ResultMesh, false);
                }

                base.complete_update();

            } catch (Exception e) {

                if ( try_count < max_tries ) {
                    cache_fill_len *= 2;
                    try_count++;
                    goto try_again;
                } else if ( try_count == max_tries ) {
                    cache_fill_len = double.MaxValue;
                    try_count++;
                    goto try_again;
                }

                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
            }
        }



        DMesh3 compute_cut(Vector3d origin, Vector3d normal, double tol, bool fill_holes, double fill_len, out bool fill_errors)
        {
            fill_errors = false;
            DMesh3 cutMesh = new DMesh3(MeshSource.GetDMeshUnsafe());

            MeshPlaneCut cut = new MeshPlaneCut(cutMesh, origin, normal);
            cut.DegenerateEdgeTol = tol;

            if (cut.Cut() == false)
                throw new Exception("[PlanarCutOp] cut.Cut() returned false");

            if (fill_holes) {

                PlanarHoleFiller fill = new PlanarHoleFiller(cut) {
                    FillTargetEdgeLen = fill_len
                };
                if (fill.Fill() == false) {
                    fill_errors = true;
                }
            }

            return cutMesh;
        }


       




        /*
         * IMeshSourceOp / DMeshSourceOp interface
         */

        public override IMesh GetIMesh()
        {
            if (base.requires_update())
                Update();
            return ResultMesh;
        }

        public override DMesh3 GetDMeshUnsafe() {
            return (DMesh3)GetIMesh();
        }

        public override bool HasSpatial {
            get { return false; }
        }
        public override ISpatial GetSpatial()
        {
            return null;
        }

        public override DMesh3 ExtractDMesh()
        {
            Update();
            var result = ResultMesh;
            ResultMesh = null;

            if ( OtherSideResultMesh != null ) {
                result.AttachMetadata("other_side", OtherSideResultMesh);
                OtherSideResultMesh = null;
            }

            base.result_consumed();
            return result;
        }



    }


}
