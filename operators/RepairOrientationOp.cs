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
    public class RepairOrientationOp : BaseDMeshSourceOp
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

        DMeshAABBTree3 CacheSpatial;
        int input_timestamp = -1;
        DMeshAABBTree3 get_cached_spatial(DMesh3 meshIn)
        {
            if (input_timestamp != meshIn.ShapeTimestamp) {
                CacheSpatial = new DMeshAABBTree3(meshIn);
                input_timestamp = meshIn.ShapeTimestamp;
            }
            return CacheSpatial;
        }



        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("GenerateClosedMeshOp: must set valid MeshSource to compute!");

            try {
                DMesh3 inputmesh = MeshSource.GetDMeshUnsafe();
                ISpatial inputSpatial = MeshSource.HasSpatial ? MeshSource.GetSpatial() : null;
                
                DMeshAABBTree3 spatial = (inputSpatial != null && inputSpatial is DMeshAABBTree3) ?
                    inputSpatial as DMeshAABBTree3 : get_cached_spatial(inputmesh);
                DMesh3 meshIn = new DMesh3(inputmesh);

                MeshRepairOrientation repair = new MeshRepairOrientation(meshIn, spatial);
                repair.OrientComponents();
                repair.SolveGlobalOrientation();

                if (invert_result)
                    meshIn.ReverseOrientation(true);

                ResultMesh = meshIn;
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
