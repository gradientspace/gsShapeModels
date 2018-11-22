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
    public class MeshAutoRepairOp : BaseDMeshSourceOp
    {

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


        public enum RemoveInsideModes
        {
            None = MeshAutoRepair.RemoveModes.None,
            Interior = MeshAutoRepair.RemoveModes.Interior,
            Occluded = MeshAutoRepair.RemoveModes.Occluded
        }
        RemoveInsideModes remove_inside_mode = RemoveInsideModes.None;
        public RemoveInsideModes InsideMode {
            get { return remove_inside_mode; }
            set { if (remove_inside_mode != value) { remove_inside_mode = value; invalidate(); } }
        }



        double min_edge_length = 0.0001;
        public virtual double MinEdgeLength {
            get { return min_edge_length; }
            set {
                double set_value = MathUtil.Clamp(value, 0.0, 1.0);
                if (Math.Abs(min_edge_length - set_value) > MathUtil.ZeroTolerancef) { min_edge_length = value; invalidate(); }
            }
        }


        int erosion_iters = 5;
        public virtual int ErosionIterations {
            get { return erosion_iters; }
            set {
                int set_value = MathUtil.Clamp(value, 0, 1000);
                if (erosion_iters != set_value) { erosion_iters = value; invalidate(); }
            }
        }


        bool invert_result = false;
        public bool InvertResult {
            get { return invert_result; }
            set {
                if (invert_result != value) { invert_result = value; invalidate(); }
            }
        }




        protected virtual void on_input_modified(ModelingOperator op)
        {
            base.invalidate();
        }


        DMesh3 ResultMesh;

        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("GenerateClosedMeshOp: must set valid MeshSource to compute!");

            try {
                DMesh3 inputmesh = MeshSource.GetDMeshUnsafe();

                DMesh3 meshIn = new DMesh3(inputmesh);

                MeshAutoRepair repair = new MeshAutoRepair(meshIn);
                repair.RemoveMode = (MeshAutoRepair.RemoveModes)(int)remove_inside_mode;
                repair.MinEdgeLengthTol = min_edge_length;
                repair.ErosionIterations = erosion_iters;
                repair.Progress = new ProgressCancel(is_invalidated);

                bool bOK = repair.Apply();

                if (bOK && invert_result)
                    meshIn.ReverseOrientation(true);

                if (is_invalidated())
                    meshIn = null;

                if (bOK) {
                    ResultMesh = meshIn;
                } else {
                    ResultMesh = base.make_failure_output(inputmesh);
                }
                base.complete_update();

            } catch (Exception e) {
                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
            }
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
            base.result_consumed();
            return result;
        }



    }


}
