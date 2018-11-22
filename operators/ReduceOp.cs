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
    public class ReduceOp : BaseDMeshSourceOp
    {
        public enum TargetModes
        {
            TriangleCount = 0,
            VertexCount = 1,
            MinEdgeLength = 2
        }
        TargetModes reduce_mode = TargetModes.TriangleCount;
        public virtual TargetModes TargetMode {
            get { return reduce_mode; }
            set {
                if (reduce_mode != value) { reduce_mode = value; invalidate(); }
            }
        }

        int triangle_count = -1;
        public virtual int TriangleCount {
            get { return triangle_count; }
            set {
                int set_value = MathUtil.Clamp(value, 1, int.MaxValue);
                if (triangle_count != set_value) { triangle_count = set_value; invalidate(); }
            }
        }

        int vertex_count = -1;
        public virtual int VertexCount {
            get { return vertex_count; }
            set {
                int set_value = MathUtil.Clamp(value, 1, int.MaxValue);
                if (vertex_count != set_value) { vertex_count = set_value; invalidate(); }
            }
        }


        double min_edge_length = 1.0;
        public virtual double MinEdgeLength {
            get { return min_edge_length; }
            set {
                double set_size = MathUtil.Clamp(value, 0.0001, 10000.0);
                if (Math.Abs(min_edge_length - set_size) > MathUtil.ZeroTolerancef) { min_edge_length = value; invalidate(); }
            }
        }

        bool reproject_to_input = false;
        public virtual bool ReprojectToInput {
            get { return reproject_to_input; }
            set { if (reproject_to_input != value) { reproject_to_input = value; invalidate(); } }
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
                throw new Exception("RemeshOp: must set valid MeshSource to compute!");

            try {
                ResultMesh = update_standard();

                base.complete_update();

            } catch (Exception e) {
                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
            }

        }


        protected virtual DMesh3 update_standard()
        {
            DMesh3 sourceMesh = MeshSource.GetDMeshUnsafe();

            ISpatial sourceSpatial = (ReprojectToInput) ? MeshSource.GetSpatial() : null;

            DMesh3 meshIn = new DMesh3(sourceMesh);

            Reducer reduce = new Reducer(meshIn);

            if (ReprojectToInput) {
                reduce.ProjectionMode = Reducer.TargetProjectionMode.AfterRefinement;
                MeshProjectionTarget target = new MeshProjectionTarget(sourceMesh, sourceSpatial);
                reduce.SetProjectionTarget(target);
            } else {
                reduce.ProjectionMode = Reducer.TargetProjectionMode.NoProjection;
            }

            switch (TargetMode) {
                case TargetModes.TriangleCount:
                    int tc = MathUtil.Clamp(TriangleCount, 1, meshIn.TriangleCount);
                    reduce.ReduceToTriangleCount(tc);
                    break;

                case TargetModes.VertexCount:
                    int tv = MathUtil.Clamp(VertexCount, 1, meshIn.VertexCount);
                    reduce.ReduceToVertexCount(tv);
                    break;

                case TargetModes.MinEdgeLength:
                    reduce.ReduceToEdgeLength(MinEdgeLength);
                    break;
            }

            reduce.Progress = new ProgressCancel(is_invalidated);

            if (is_invalidated())
                return null;

            return meshIn;
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
