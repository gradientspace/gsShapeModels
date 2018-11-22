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

    public  class SeparateSolidsOp : BaseSingleOutputModelingOperator, ResultSourceOp<List<DMesh3>>
    {
        bool group_nested_shells = false;
        public bool GroupNestedShells {
            get { return group_nested_shells; }
            set {
                if (group_nested_shells != value) { group_nested_shells = value; invalidate(); }
            }
        }

        bool orient_nested_shells = true;
        public bool OrientNestedShells {
            get { return orient_nested_shells; }
            set {
                if (orient_nested_shells != value) { orient_nested_shells = value; invalidate(); }
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


        List<DMesh3> ResultMeshes;

        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("SeparateSolidsOp: must set valid MeshSource to compute!");

            ResultMeshes = null;

            try {
                DMesh3 meshIn = new DMesh3(MeshSource.GetDMeshUnsafe());

                MeshConnectedComponents comp = new MeshConnectedComponents(meshIn);
                comp.FindConnectedT();
                DSubmesh3Set subMeshes = new DSubmesh3Set(meshIn, comp);

                List<DMesh3> curMeshes = new List<DMesh3>();
                foreach (var submesh in subMeshes)
                    curMeshes.Add(submesh.SubMesh);

                if (group_nested_shells) {
                    MeshSpatialSort sort = new MeshSpatialSort();
                    foreach (var mesh in curMeshes)
                        sort.AddMesh(mesh, mesh);
                    sort.Sort();

                    curMeshes.Clear();
                    foreach (var solid in sort.Solids) {
                        DMesh3 outer = solid.Outer.Mesh;
                        if (orient_nested_shells && is_outward_oriented(outer) == false)
                            outer.ReverseOrientation();

                        foreach (var hole in solid.Cavities) {
                            if (orient_nested_shells && hole.Mesh.CachedIsClosed && is_outward_oriented(hole.Mesh) == true)
                                hole.Mesh.ReverseOrientation();
                            MeshEditor.Append(outer, hole.Mesh);
                        }

                        curMeshes.Add(outer);
                    }
                }

                ResultMeshes = curMeshes;

                base.complete_update();

            } catch (Exception) {
                ResultMeshes = new List<DMesh3>();
                base.complete_update();
                throw;
            }
        }



        bool is_outward_oriented(DMesh3 mesh)
        {
            AxisAlignedBox3d bounds = mesh.CachedBounds;
            Vector3d p = bounds.Center + 2 * bounds.Diagonal;
            double wn = mesh.WindingNumber(p);
            if (wn < -0.95)
                return false;
            return true;
        }





        /*
         * DMeshListSourceOp interface
         */

        public virtual List<DMesh3> ExtractResult()
        {
            Update();
            var result = ResultMeshes;
            ResultMeshes = null;
            base.result_consumed();
            return result;
        }



    }


}
