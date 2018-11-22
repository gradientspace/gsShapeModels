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
    public class MeshMorphologyOp : BaseDMeshSourceOp
    {
        public enum OperationTypes
        {
            Dilate = 0,
            Contract = 1,
            Open = 2,
            Close = 3
        }
        OperationTypes op_type = OperationTypes.Open;
        public OperationTypes OpType {
            get { return op_type; }
            set { if (op_type != value) { op_type = value; invalidate(); } }
        }

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

                ResultMesh = update_step_1();

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
        MeshSignedDistanceGrid cached_sdf;
        AxisAlignedBox3d cached_sdf_bounds;


        protected virtual DMesh3 update_step_1()
        {
            double unsigned_offset = Math.Abs(distance);
            if (cached_sdf == null ||
                unsigned_offset > cached_sdf_max_offset ||
                grid_cell_size != cached_sdf.CellSize) 
            {
                DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
                int exact_cells = (int)(unsigned_offset / grid_cell_size) + 1;

                // only use spatial DS if we are computing enough cells
                DMeshAABBTree3 use_spatial = GenerateClosedMeshOp.MeshSDFShouldUseSpatial(
                    input_spatial, exact_cells, grid_cell_size, input_mesh_edge_stats.z);
                MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(meshIn, grid_cell_size, use_spatial) {
                    ExactBandWidth = exact_cells
                };
                if (use_spatial != null) {
                    sdf.NarrowBandMaxDistance = unsigned_offset + grid_cell_size;
                    sdf.ComputeMode = MeshSignedDistanceGrid.ComputeModes.NarrowBand_SpatialFloodFill;
                }

                sdf.CancelF = is_invalidated;
                sdf.Compute();
                if (is_invalidated())
                    return null;
                cached_sdf = sdf;
                cached_sdf_max_offset = unsigned_offset;
                cached_sdf_bounds = meshIn.CachedBounds;
            }

            var iso = new DenseGridTrilinearImplicit(cached_sdf.Grid, cached_sdf.GridOrigin, cached_sdf.CellSize);
            MarchingCubes c = new MarchingCubes();
            c.Implicit = iso;

            if (op_type == OperationTypes.Contract || op_type == OperationTypes.Open)
                c.IsoValue = -distance;
            else
                c.IsoValue = distance;

            c.Bounds = cached_sdf_bounds;
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(distance + 3 * c.CubeSize);
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;
            c.RootModeSteps = 5;

            c.CancelF = is_invalidated;
            c.Generate();
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

            if (op_type == OperationTypes.Open || op_type == OperationTypes.Close)
                return update_step_2(c.Mesh);
            else
                return c.Mesh;
        }





        protected virtual DMesh3 update_step_2(DMesh3 meshIn)
        {
            double unsigned_offset = Math.Abs(distance);
            int exact_cells = (int)(unsigned_offset / grid_cell_size) + 1;

            // only use spatial DS if we are computing enough cells
            bool compute_spatial = GenerateClosedMeshOp.MeshSDFShouldUseSpatial(
                input_spatial, exact_cells, grid_cell_size, input_mesh_edge_stats.z) != null;
            DMeshAABBTree3 use_spatial = (compute_spatial) ? new DMeshAABBTree3(meshIn, true) : null;
            MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(meshIn, grid_cell_size, use_spatial) {
                ExactBandWidth = exact_cells
            };
            if (use_spatial != null) {
                sdf.NarrowBandMaxDistance = unsigned_offset + grid_cell_size;
                sdf.ComputeMode = MeshSignedDistanceGrid.ComputeModes.NarrowBand_SpatialFloodFill;
            }

            sdf.CancelF = is_invalidated;
            sdf.Compute();
            if (is_invalidated())
                return null;

            var iso = new DenseGridTrilinearImplicit(sdf.Grid, sdf.GridOrigin, sdf.CellSize);
            MarchingCubes c = new MarchingCubes();
            c.Implicit = iso;

            if (op_type == OperationTypes.Close)
                c.IsoValue = -distance;
            else
                c.IsoValue = distance;

            c.Bounds = cached_sdf_bounds;
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(distance + 3 * c.CubeSize);
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;
            c.RootModeSteps = 5;

            c.CancelF = is_invalidated;
            c.Generate();
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
