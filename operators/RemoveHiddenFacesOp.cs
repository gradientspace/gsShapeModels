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
    public class RemoveHiddenFacesOp : BaseDMeshSourceOp
    {
        public enum CalculationMode
        {
            RayParity = RemoveOccludedTriangles.CalculationMode.RayParity,
            WindingNumber = RemoveOccludedTriangles.CalculationMode.FastWindingNumber,
            SimpleOcclusionTest = RemoveOccludedTriangles.CalculationMode.SimpleOcclusionTest
        }
        CalculationMode inside_mode = CalculationMode.WindingNumber;
        public CalculationMode InsideMode {
            get { return inside_mode; }
            set { if (inside_mode != value) { inside_mode = value; invalidate(); } }
        }



        bool all_hidden_vertices = false;
        public bool AllHiddenVertices {
            get { return all_hidden_vertices; }
            set {
                if (all_hidden_vertices != value) { all_hidden_vertices = value; invalidate(); }
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

        DSubmesh3 RemovedSubmesh;
        List<int> RemovedTris;

        DMeshAABBTreePro cachedSpatial;
        int spatial_cache_timstamp = -1;



        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("RemoveHiddenFacesOp: must set valid MeshSource to compute!");

            try {
                DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
                if (cachedSpatial == null || spatial_cache_timstamp != meshIn.ShapeTimestamp) {
                    cachedSpatial = new DMeshAABBTreePro(meshIn, true);
                    cachedSpatial.FastWindingNumber(Vector3d.Zero);
                    spatial_cache_timstamp = meshIn.ShapeTimestamp;
                }
                DMesh3 editMesh = new DMesh3(meshIn);

                RemoveOccludedTriangles remove = new RemoveOccludedTriangles(editMesh, cachedSpatial) {
                    InsideMode = (RemoveOccludedTriangles.CalculationMode)(int)inside_mode,
                    PerVertex = all_hidden_vertices
                };
                remove.Progress = new ProgressCancel(is_invalidated);

                if (remove.Apply() == false) {
                    ResultMesh = null;
                    RemovedTris = null;
                    RemovedSubmesh = null;
                } else {
                    ResultMesh = editMesh;
                    RemovedTris = remove.RemovedT;
                    RemovedSubmesh = new DSubmesh3(MeshSource.GetDMeshUnsafe(), RemovedTris);
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

            result.AttachMetadata("removed_tris", RemovedTris);
            result.AttachMetadata("removed_submesh", RemovedSubmesh);

            base.result_consumed();
            return result;
        }



    }


}
