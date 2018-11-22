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
    public class MeshWrapOp : BaseDMeshSourceOp
    {
        double grid_cell_size = 1.0;
        public double GridCellSize {
            get { return grid_cell_size; }
            set {
                double set_size = MathUtil.Clamp(value, 0.0001, 10000.0);
                if (Math.Abs(grid_cell_size - set_size) > MathUtil.ZeroTolerancef) { grid_cell_size = value; invalidate(); }
            }
        }


        double mesh_cell_size = 1.0;
        public double MeshCellSize {
            get { return mesh_cell_size; }
            set {
                double set_size = MathUtil.Clamp(value, 0.0001, 10000.0);
                if (Math.Abs(mesh_cell_size - set_size) > MathUtil.ZeroTolerancef) { mesh_cell_size = value; invalidate(); }
            }
        }


        double distance = 1.0;
        public double Distance {
            get { return distance; }
            set {
                distance = MathUtil.Clamp(distance, 0, 999999);
                if (Math.Abs(distance - value) > MathUtil.ZeroTolerancef) { distance = value; invalidate(); }
            }
        }


        double min_component_volume = 1.0;
        public double MinComponentVolume {
            get { return min_component_volume; }
            set {
                double set_value = MathUtil.Clamp(value, 0.0, 1000);
                if (Math.Abs(min_component_volume - set_value) > MathUtil.ZeroTolerancef) { min_component_volume = set_value; invalidate(); }
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
        bool cached_is_closed;
        int input_mesh_cache_timestamp = -1;
        Vector3d input_mesh_edge_stats = new Vector3d(1, 1, 1);
        DMeshAABBTree3 input_spatial;


        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("MeshMorphologyOp: must set valid MeshSource to compute!");

            try {
                ResultMesh = null;

                DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
                input_spatial = MeshSource.GetSpatial() as DMeshAABBTree3;
                if (meshIn.ShapeTimestamp != input_mesh_cache_timestamp) {
                    cached_is_closed = meshIn.IsClosed();
                    MeshQueries.EdgeLengthStats(meshIn, out input_mesh_edge_stats.x, out input_mesh_edge_stats.y, out input_mesh_edge_stats.z);
                    if (input_spatial == null)
                        input_spatial = new DMeshAABBTree3(meshIn, false);
                    input_mesh_cache_timestamp = meshIn.ShapeTimestamp;
                }

                ResultMesh = compute_wrap();

                if (ResultMesh.TriangleCount == 0)
                    ResultMesh = base.make_failure_output(null);

                base.complete_update();

            } catch (Exception e) {
                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
            }

        }



        // level-set cache info
        double cached_sdf_max_offset = 0;
        CachingMeshSDF cached_sdf;
        AxisAlignedBox3d cached_sdf_bounds;


        protected virtual DMesh3 compute_wrap()
        {
            DMesh3 meshIn = MeshSource.GetDMeshUnsafe();

            double unsigned_offset = Math.Abs(distance);
            if (cached_sdf == null ||
                unsigned_offset > cached_sdf_max_offset ||
                grid_cell_size != cached_sdf.CellSize) 
            {
                DMeshAABBTree3 use_spatial = input_spatial;
                CachingMeshSDF sdf = new CachingMeshSDF(meshIn, grid_cell_size, use_spatial);
                sdf.MaxOffsetDistance = 2 * (float)unsigned_offset;

                sdf.CancelF = is_invalidated;
                sdf.Initialize();
                if (is_invalidated())
                    return null;

                cached_sdf = sdf;
                cached_sdf_max_offset = unsigned_offset;
                cached_sdf_bounds = meshIn.CachedBounds;
            }

            var grid_iso = new CachingMeshSDFImplicit(cached_sdf);
            // currently MCPro-Continuation does not work w/ non-zero
            //   isovalues, so we have to shift our target offset externally
            var iso = new ImplicitOffset3d() { A = grid_iso, Offset = distance };

            MarchingCubesPro c = new MarchingCubesPro();
            c.Implicit = iso;
            c.Bounds = cached_sdf_bounds;
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(distance + 3 * c.CubeSize);

            c.CancelF = is_invalidated;
            c.GenerateContinuation(offset_seeds(meshIn,distance));
            if (is_invalidated())
                return null;

            Reducer r = new Reducer(c.Mesh);
            r.FastCollapsePass(c.CubeSize*0.5, 3, true);
            if (is_invalidated())
                return null;

            if (min_component_volume > 0)
                MeshEditor.RemoveSmallComponents(c.Mesh, min_component_volume, min_component_volume);
            if (is_invalidated())
                return null;

            DMesh3 offsetMesh = c.Mesh;

            MeshConnectedComponents comp = new MeshConnectedComponents(offsetMesh);
            comp.FindConnectedT();
            if (is_invalidated())
                return null;
            DSubmesh3Set subMeshes = new DSubmesh3Set(offsetMesh, comp);
            if (is_invalidated())
                return null;

            MeshSpatialSort sort = new MeshSpatialSort();
            foreach (var subMesh in subMeshes)
                sort.AddMesh(subMesh.SubMesh, subMesh);
            sort.Sort();
            if (is_invalidated())
                return null;

            DMesh3 outerMesh = new DMesh3();
            foreach (var solid in sort.Solids) {
                DMesh3 outer = solid.Outer.Mesh;
                //if (is_outward_oriented(outer) == false)
                //    outer.ReverseOrientation();
                MeshEditor.Append(outerMesh, outer);
            }
            if (is_invalidated())
                return null;

            return compute_inset(outerMesh);
        }



        IEnumerable<Vector3d> offset_seeds(DMesh3 meshIn, double offset)
        {
            foreach (int tid in meshIn.TriangleIndices()) {
                Vector3d n, c; double a;
                meshIn.GetTriInfo(tid, out n, out a, out c);
                yield return c + offset * n;
            }
        }




        protected virtual DMesh3 compute_inset(DMesh3 meshIn)
        {
            double unsigned_offset = Math.Abs(distance);

            DMeshAABBTree3 use_spatial = new DMeshAABBTree3(meshIn, true);
            if (is_invalidated())
                return null;

            CachingMeshSDF sdf = new CachingMeshSDF(meshIn, grid_cell_size, use_spatial);
            sdf.MaxOffsetDistance = (float)unsigned_offset;
            sdf.CancelF = is_invalidated;
            sdf.Initialize();
            if (is_invalidated())
                return null;

            var sdf_iso = new CachingMeshSDFImplicit(sdf);
            // currently MCPro-Continuation does not work w/ non-zero
            //   isovalues, so we have to shift our target offset externally
            ImplicitOffset3d iso = new ImplicitOffset3d() { A = sdf_iso, Offset = -distance };

            MarchingCubesPro c = new MarchingCubesPro();
            c.Implicit = iso;
            c.Bounds = cached_sdf_bounds;
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(distance + 3 * c.CubeSize);

            c.CancelF = is_invalidated;
            c.GenerateContinuation(offset_seeds(meshIn, -distance));
            if (is_invalidated())
                return null;

            Reducer r = new Reducer(c.Mesh);
            r.FastCollapsePass(c.CubeSize * 0.5, 3, true);
            if (is_invalidated())
                return null;

            if (min_component_volume > 0)
                MeshEditor.RemoveSmallComponents(c.Mesh, min_component_volume, min_component_volume);
            if (is_invalidated())
                return null;

            return c.Mesh;
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
