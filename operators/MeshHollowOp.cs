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
    public class MeshHollowOp : BaseDMeshSourceOp
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


        double wall_thickness = 2.0;
        public double WallThickness {
            get { return wall_thickness; }
            set {
                if (Math.Abs(wall_thickness - value) > MathUtil.ZeroTolerancef) { wall_thickness = value; invalidate(); }
            }
        }


        bool enable_infill = false;
        public bool EnableInfill {
            get { return enable_infill; }
            set {
                if (value != enable_infill) { enable_infill = value; invalidate(); }
            }
        }


        double infill_thickness = 2.0;
        public double InfillThickness {
            get { return infill_thickness; }
            set {
                if (Math.Abs(infill_thickness - value) > MathUtil.ZeroTolerancef) { infill_thickness = value; invalidate(); }
            }
        }

        double infill_spacing = 10.0;
        public double InfillSpacing {
            get { return infill_spacing; }
            set {
                if (Math.Abs(infill_spacing - value) > MathUtil.ZeroTolerancef) { infill_spacing = value; invalidate(); }
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
                throw new Exception("MeshShellOp: must set valid MeshSource to compute!");

            try {
                ResultMesh = null;

                DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
                input_spatial = MeshSource.GetSpatial() as DMeshAABBTree3;
                if ( meshIn.ShapeTimestamp != input_mesh_cache_timestamp ) {
                    cached_is_closed = meshIn.IsClosed();
                    MeshQueries.EdgeLengthStats(meshIn, out input_mesh_edge_stats.x, out input_mesh_edge_stats.y, out input_mesh_edge_stats.z);
                    if (input_spatial == null)
                        input_spatial = new DMeshAABBTree3(meshIn, false);
                    input_mesh_cache_timestamp = meshIn.ShapeTimestamp;
                }

                compute_hollow();

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


        protected virtual void compute_hollow()
        {
            double offset_distance = -wall_thickness;

            if (cached_sdf == null ||
                wall_thickness > cached_sdf_max_offset ||
                grid_cell_size != cached_sdf.CellSize) {
                DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
                int exact_cells = (int)((wall_thickness) / grid_cell_size) + 1;

                // only use spatial DS if we are computing enough cells
                DMeshAABBTree3 use_spatial = GenerateClosedMeshOp.MeshSDFShouldUseSpatial(input_spatial, exact_cells, grid_cell_size, input_mesh_edge_stats.z);
                MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(meshIn, grid_cell_size, use_spatial) {
                    ExactBandWidth = exact_cells
                };
                if ( use_spatial != null ) {
                    sdf.NarrowBandMaxDistance = wall_thickness + grid_cell_size;
                    sdf.ComputeMode = MeshSignedDistanceGrid.ComputeModes.NarrowBand_SpatialFloodFill;
                } 

                sdf.CancelF = is_invalidated;
                sdf.Compute();
                if (is_invalidated())
                    return;
                cached_sdf = sdf;
                cached_sdf_max_offset = wall_thickness;
                cached_sdf_bounds = meshIn.CachedBounds;
            }

            var iso = new DenseGridTrilinearImplicit(cached_sdf.Grid, cached_sdf.GridOrigin, cached_sdf.CellSize);
            ImplicitOffset3d shell_field = new ImplicitOffset3d() { A = iso, Offset = offset_distance };
            ImplicitFunction3d use_iso = shell_field;


            if (enable_infill) {
                GridDistanceField grid_df = new GridDistanceField() {
                    CellSize = infill_spacing,
                    Radius = infill_thickness*0.5,
                    Origin = cached_sdf.GridOrigin
                };
                ImplicitDifference3d diff = new ImplicitDifference3d() {
                    A = shell_field, B = grid_df
                };
                use_iso = diff;
            }


            MarchingCubes c = new MarchingCubes();
            c.Implicit = use_iso;
            c.IsoValue = 0;
            c.Bounds = cached_sdf_bounds;
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(offset_distance + 3 * c.CubeSize);
            c.RootMode = MarchingCubes.RootfindingModes.LerpSteps;
            c.RootModeSteps = 5;

            c.CancelF = is_invalidated;
            c.Generate();
            if (is_invalidated())
                return;

            Reducer r = new Reducer(c.Mesh);
            r.FastCollapsePass(c.CubeSize * 0.5, 3, true);
            if (is_invalidated())
                return;

            //r.ReduceToTriangleCount(c.Mesh.TriangleCount / 5);
            //if (is_invalidated())
            //    return;

            if (min_component_volume > 0)
                MeshEditor.RemoveSmallComponents(c.Mesh, min_component_volume, min_component_volume);
            if (is_invalidated())
                return;

            c.Mesh.AttachMetadata("is_partial", new object());

            ResultMesh = c.Mesh;
        }




        class GridDistanceField : BoundedImplicitFunction3d
        {
            public double CellSize;
            public double Radius;
            public Vector3d Origin;
            public Quaterniond Rotation;

            public GridDistanceField()
            {
                Origin = -100*Vector3d.One;
                Rotation = Quaterniond.AxisAngleD(Vector3d.AxisX, 45) * Quaterniond.AxisAngleD(Vector3d.AxisZ, 45);
                CellSize = 8.0f;
                Radius = 1.0f;
            }

            public double Value(ref Vector3d ptIn)
            {
                Vector3d pt = Rotation * (ptIn - Origin);
                Vector3d gridPt = new Vector3d(pt.x/CellSize, pt.y/CellSize, pt.z/CellSize);

                // compute unit-box coordinates
                gridPt.x -= (double)(int)gridPt.x;
                gridPt.y -= (double)(int)gridPt.y;
                gridPt.z -= (double)(int)gridPt.z;

                double lx = gridPt.x - ((gridPt.x < 0.5) ? 0.0 : 1.0);  lx *= lx;
                double ly = gridPt.y - ((gridPt.y < 0.5) ? 0.0 : 1.0);  ly *= ly;
                double lz = gridPt.z - ((gridPt.z < 0.5) ? 0.0 : 1.0);  lz *= lz;

                double d_sqr = MathUtil.Min(lx+ly, lx+lz, ly+lz);
                double d = Math.Sqrt(d_sqr);
                d *= CellSize;
                return (d - Radius);
            }


            public AxisAlignedBox3d Bounds()
            {
                return new AxisAlignedBox3d(Origin, 10 * CellSize);
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
