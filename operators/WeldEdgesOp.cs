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
    public class WeldEdgesOp : BaseDMeshSourceOp
    {

        bool only_unique_pairs = false;
        public bool OnlyUniquePairs {
            get { return only_unique_pairs; }
            set {
                if (only_unique_pairs != value) { only_unique_pairs = value; invalidate(); }
            }
        }



        double merge_tolerance = MathUtil.ZeroTolerancef;
        public double MergeTolerance {
            get { return merge_tolerance; }
            set {
                if (Math.Abs(merge_tolerance - value) > MathUtil.ZeroTolerance) { merge_tolerance = value; invalidate(); }
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


        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("GenerateClosedMeshOp: must set valid MeshSource to compute!");

            try {
                DMesh3 meshIn = new DMesh3(MeshSource.GetDMeshUnsafe());
                MergeCoincidentEdges merge = new MergeCoincidentEdges(meshIn) {
                    OnlyUniquePairs = this.OnlyUniquePairs,
                    MergeDistance = this.MergeTolerance
                };
                if (merge.Apply() == false)
                    throw new Exception("merge.Apply() returned false");

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
