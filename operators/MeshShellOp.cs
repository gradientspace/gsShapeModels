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
    public class MeshShellOp : BaseDMeshSourceOp
    {
        public enum ShellTypes
        {
            Extrusion = 0,
            DistanceField = 1
        }
        ShellTypes shell_type = ShellTypes.DistanceField;
        public ShellTypes ShellType {
            get { return shell_type; }
            set { if (shell_type != value) { shell_type = value; invalidate(); } }
        }


        public enum ShellDirections
        {
            Inner = 0,
            Outer = 1,
            Symmetric = 2
        }
        ShellDirections shell_direction = ShellDirections.Inner;
        public ShellDirections ShellDirection {
            get { return shell_direction; }
            set { if (shell_direction != value) { shell_direction = value; invalidate(); } }
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


        double shell_thickness = 2.0;
        public double ShellThickness {
            get { return shell_thickness; }
            set {
                if (Math.Abs(shell_thickness - value) > MathUtil.ZeroTolerancef) { shell_thickness = value; invalidate(); }
            }
        }


        bool shell_surface_only = false;
        public bool ShellSurfaceOnly {
            get { return shell_surface_only; }
            set {
                if (shell_surface_only != value) { shell_surface_only = value; invalidate(); }
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

                if (shell_type == ShellTypes.DistanceField)
                    compute_shell_distancefield();
                else
                    compute_shell_extrude();

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


        protected virtual void compute_shell_distancefield()
        {
            if (cached_is_closed == false ) {
                compute_shell_distancefield_unsigned();
                return;
            }

            double offset_distance = shell_thickness;
            Interval1d shell_range = new Interval1d(0, offset_distance);
            if (shell_direction == ShellDirections.Symmetric) {
                shell_range = new Interval1d(-offset_distance / 2, offset_distance / 2);
            } else if (shell_direction == ShellDirections.Inner) {
                shell_range = new Interval1d(-offset_distance, 0);
                offset_distance = -offset_distance;
            }


            if (cached_sdf == null ||
                shell_thickness > cached_sdf_max_offset ||
                grid_cell_size != cached_sdf.CellSize) {
                DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
                int exact_cells = (int)((shell_thickness) / grid_cell_size) + 1;

                // only use spatial DS if we are computing enough cells
                DMeshAABBTree3 use_spatial = GenerateClosedMeshOp.MeshSDFShouldUseSpatial(input_spatial, exact_cells, grid_cell_size, input_mesh_edge_stats.z);
                MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(meshIn, grid_cell_size, use_spatial) {
                    ExactBandWidth = exact_cells
                };
                if ( use_spatial != null ) {
                    sdf.NarrowBandMaxDistance = shell_thickness + grid_cell_size;
                    sdf.ComputeMode = MeshSignedDistanceGrid.ComputeModes.NarrowBand_SpatialFloodFill;
                } 

                sdf.CancelF = is_invalidated;
                sdf.Compute();
                if (is_invalidated())
                    return;
                cached_sdf = sdf;
                cached_sdf_max_offset = shell_thickness;
                cached_sdf_bounds = meshIn.CachedBounds;
            }

            var iso = new DenseGridTrilinearImplicit(cached_sdf.Grid, cached_sdf.GridOrigin, cached_sdf.CellSize);
            BoundedImplicitFunction3d shell_field = (shell_direction == ShellDirections.Symmetric) ?
                (BoundedImplicitFunction3d)new ImplicitShell3d() { A = iso, Inside = shell_range } :
                (BoundedImplicitFunction3d)new ImplicitOffset3d() { A = iso, Offset = offset_distance };
            //var shell_field = new ImplicitShell3d() { A = iso, Inside = shell_range };
            //BoundedImplicitFunction3d shell_field = (signed_field) ?
            //    (BoundedImplicitFunction3d)new ImplicitShell3d() { A = iso, Inside = shell_range } :
            //    (BoundedImplicitFunction3d)new ImplicitOffset3d() { A = iso, Offset = offset_distance };
            //ImplicitOffset3d offset = new ImplicitOffset3d() { A = iso, Offset = offset_distance };

            MarchingCubes c = new MarchingCubes();
            c.Implicit = shell_field;
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

            if (min_component_volume > 0)
                MeshEditor.RemoveSmallComponents(c.Mesh, min_component_volume, min_component_volume);
            if (is_invalidated())
                return;

            if (shell_surface_only) {
                if (shell_direction == ShellDirections.Inner || shell_direction == ShellDirections.Outer)
                    c.Mesh.AttachMetadata("is_partial", new object());
            }

            ResultMesh = c.Mesh;
        }






        protected virtual void compute_shell_distancefield_unsigned()
        {
            double offset_distance = shell_thickness * 0.5;

            if (cached_sdf == null ||
                offset_distance > cached_sdf_max_offset ||
                grid_cell_size != cached_sdf.CellSize) {
                DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
                int exact_cells = (int)((offset_distance) / grid_cell_size) + 1;
                MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(meshIn, grid_cell_size) {
                    ExactBandWidth = exact_cells,
                    ComputeSigns = false
                };
                sdf.CancelF = is_invalidated;
                sdf.Compute();
                if (is_invalidated())
                    return;
                cached_sdf = sdf;
                cached_sdf_max_offset = offset_distance;
                cached_sdf_bounds = meshIn.CachedBounds;
            }

            var iso = new DenseGridTrilinearImplicit(cached_sdf.Grid, cached_sdf.GridOrigin, cached_sdf.CellSize);
            MarchingCubes c = new MarchingCubes();
            c.Implicit = iso;
            c.IsoValue = offset_distance;
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

            if (min_component_volume > 0)
                MeshEditor.RemoveSmallComponents(c.Mesh, min_component_volume, min_component_volume);
            if (is_invalidated())
                return;

            ResultMesh = c.Mesh;
        }






        protected virtual void compute_shell_extrude()
        {
            DMesh3 mesh = new DMesh3(MeshSource.GetDMeshUnsafe());
            MeshNormals.QuickCompute(mesh);

            if (shell_direction == ShellDirections.Symmetric) {
                double thickness = shell_thickness * 0.5;

                DMesh3 outerMesh = new DMesh3(mesh);
                MeshExtrudeMesh outerExtrude = new MeshExtrudeMesh(outerMesh);
                outerExtrude.ExtrudedPositionF = (v, n, vid) => {
                    return v + thickness * (Vector3d)n;
                };
                if (outerExtrude.Extrude() == false)
                    throw new Exception("MeshShellOp.compute_shell_extrude: outer Extrude() returned false!");
                MeshEditor.RemoveTriangles(outerMesh, outerExtrude.InitialTriangles);

                MeshExtrudeMesh innerExtrude = new MeshExtrudeMesh(mesh);
                innerExtrude.IsPositiveOffset = false;
                innerExtrude.ExtrudedPositionF = (v, n, vid) => {
                    return v - thickness * (Vector3d)n;
                };
                if (innerExtrude.Extrude() == false)
                    throw new Exception("MeshShellOp.compute_shell_extrude: inner Extrude() returned false!");
                MeshEditor.RemoveTriangles(mesh, innerExtrude.InitialTriangles);

                MeshEditor.Append(mesh, outerMesh);

                if (cached_is_closed == false ) {
                    // cheating!
                    MergeCoincidentEdges merge = new MergeCoincidentEdges(mesh);
                    merge.Apply();
                }
                

            } else {
                double thickness = (shell_direction == ShellDirections.Outer) ?
                    shell_thickness : -shell_thickness;

                MeshExtrudeMesh extrude = new MeshExtrudeMesh(mesh);
                extrude.IsPositiveOffset = (shell_direction == ShellDirections.Outer);

                extrude.ExtrudedPositionF = (v, n, vid) => {
                    return v + thickness * (Vector3d)n;
                };

                if (extrude.Extrude() == false)
                    throw new Exception("MeshShellOp.compute_shell_extrude: Extrude() returned false!");

                if (shell_surface_only && cached_is_closed) {
                    MeshEditor.RemoveTriangles(mesh, extrude.InitialTriangles);
                    if (shell_direction == ShellDirections.Inner)
                        mesh.ReverseOrientation();
                    if (shell_direction == ShellDirections.Inner || shell_direction == ShellDirections.Outer)
                        mesh.AttachMetadata("is_partial", new object());
                }
            }

            if (is_invalidated())
                return;

            ResultMesh = mesh;
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
