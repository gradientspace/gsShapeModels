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
    public class CombineMeshesOp : BaseDMeshSourceOp
    {
        bool orient_nested_shells = false;
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


        DMesh3 ResultMesh;

        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("CombineMeshesOp: must set valid MeshSource to compute!");

            ResultMesh = null;

            try {
                DMesh3 meshIn = new DMesh3(MeshSource.GetDMeshUnsafe());

                if ( orient_nested_shells ) { 
                    MeshConnectedComponents comp = new MeshConnectedComponents(meshIn);
                    comp.FindConnectedT();
                    DSubmesh3Set subMeshes = new DSubmesh3Set(meshIn, comp);

                    List<DMesh3> curMeshes = new List<DMesh3>();
                    foreach (var submesh in subMeshes)
                        curMeshes.Add(submesh.SubMesh);

                    MeshSpatialSort sort = new MeshSpatialSort();
                    foreach (var mesh in curMeshes)
                        sort.AddMesh(mesh, mesh);
                    sort.Sort();

                    ResultMesh = new DMesh3();
                    MeshEditor editor = new MeshEditor(ResultMesh);
                    foreach (var solid in sort.Solids) {
                        DMesh3 outer = solid.Outer.Mesh;
                        if (!is_outward_oriented(outer))
                            outer.ReverseOrientation();
                        editor.AppendMesh(outer, ResultMesh.AllocateTriangleGroup());

                        foreach (var hole in solid.Cavities) {
                            if (hole.Mesh.CachedIsClosed && is_outward_oriented(hole.Mesh) == true)
                                hole.Mesh.ReverseOrientation();
                            editor.AppendMesh(hole.Mesh, ResultMesh.AllocateTriangleGroup());
                        }
                    }

                } else {
                    ResultMesh = meshIn;
                }

                base.complete_update();

            } catch (Exception e) {
                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
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
