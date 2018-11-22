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
    public class GenerateClosedMeshOp : BaseDMeshSourceOp
    {
        public enum ClosingTypes
        {
            LevelSet = 0,
            WindingNumberGrid = 1,
            WindingNumberAnalytic = 2
        }
        ClosingTypes closing_type = ClosingTypes.LevelSet;
        public ClosingTypes ClosingType {
            get { return closing_type; }
            set { if (closing_type != value) { closing_type = value; invalidate(); } }
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


        double offset_distance = 0.0;
        public double OffsetDistance {
            get { return offset_distance; }
            set {
                if (Math.Abs(offset_distance - value) > MathUtil.ZeroTolerancef) { offset_distance = value; invalidate(); }
            }
        }


        double winding_iso = 0.5;
        public double WindingIsoValue {
            get { return winding_iso; }
            set {
                double set_iso = MathUtil.Clamp(value, 0.01, 0.99);
                if (Math.Abs(winding_iso - set_iso) > MathUtil.ZeroTolerancef) { winding_iso = set_iso; invalidate(); }
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
                throw new Exception("GenerateClosedMeshOp: must set valid MeshSource to compute!");

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

                if (closing_type == ClosingTypes.LevelSet) {
                    update_level_set();
                } else if (closing_type == ClosingTypes.WindingNumberGrid) {
                    if (cached_is_closed)
                        update_winding();
                    else
                        update_winding_fast();
                } else {
                    update_winding_exact();
                }

                base.complete_update();

            } catch (Exception e) {
                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
            }

        }




        // MWN cache info
        DMeshAABBTreePro spatialPro;
        int spatial_timestamp = -1;

        MeshScalarSamplingGrid cached_mwn_grid;
        AxisAlignedBox3d cached_mwn_bounds;


        /// <summary>
        /// Sample analytic winding number into grid in narrow-band around target isovalue, 
        /// and then extract using marching cubes.
        /// 
        /// TODO: don't need to discard current grid when isovalue changes, just need to
        ///   re-run the front propagation part of mwn.Compute()!
        ///   If this is done, then use this instead of update_winding_fast() for open meshes
        /// </summary>
        protected virtual void update_winding()
        {
            DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
            if (spatialPro == null || spatial_timestamp != meshIn.ShapeTimestamp) {
                spatialPro = new DMeshAABBTreePro(meshIn, true);
                spatialPro.FastWindingNumber(Vector3d.Zero);
                spatial_timestamp = meshIn.ShapeTimestamp;
            }
            if (is_invalidated())
                return;

            if (cached_mwn_grid == null ||
                grid_cell_size != cached_mwn_grid.CellSize ||
                (float)winding_iso != cached_mwn_grid.IsoValue) 
            {
                Func<Vector3d, double> fastWN = (q) => { return spatialPro.FastWindingNumber(q); };
                var mwn = new MeshScalarSamplingGrid(meshIn, grid_cell_size, fastWN) { IsoValue = (float)winding_iso };
                mwn.CancelF = is_invalidated;
                mwn.Compute();
                if (is_invalidated()) 
                    return;
                cached_mwn_grid = mwn;
                cached_mwn_bounds = meshIn.CachedBounds;
            }

            MarchingCubes c = new MarchingCubes();
            c.Implicit = new SampledGridImplicit(cached_mwn_grid);
            c.IsoValue = 0.0;
            c.Bounds = cached_mwn_bounds;
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(3 * c.CubeSize);
            c.RootMode = MarchingCubes.RootfindingModes.Bisection;
            c.RootModeSteps = 10;

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

            // reproject - if we want to do this, we need to create spatial and meshIn above!
            gParallel.ForEach(c.Mesh.VertexIndices(), (vid) => {
                if (is_invalidated())
                    return;
                Vector3d v = c.Mesh.GetVertex(vid);
                int tid = spatialPro.FindNearestTriangle(v, grid_cell_size * MathUtil.SqrtTwo);
                if (tid != DMesh3.InvalidID) {
                    var query = MeshQueries.TriangleDistance(meshIn, tid, v);
                    if (v.Distance(query.TriangleClosest) < grid_cell_size)
                        c.Mesh.SetVertex(vid, query.TriangleClosest);
                }
            });

            if (is_invalidated())
                return;

            ResultMesh = c.Mesh;
        }






        CachingDenseGridTrilinearImplicit cached_lazy_mwn_grid;


        /// <summary>
        /// this variant use a lazy-evaluation version of the grid, and continuation-MC,
        /// so the marching cubes pulls values from the grid that are evaluated on-the-fly.
        /// In theory this should be comparable or faster than the narrow-band version,
        /// practice it is 10-20% slower...?? possible reasons:
        ///    - spinlock contention
        ///    - lots of duplicate grid evaluations because CachingDenseGridTrilinearImplicit does not use locking
        /// 
        /// However, because it can re-use grid values, changing the isovalue is 
        /// *much* faster and so it makes sense if the mesh is not closed...
        /// </summary>
        protected virtual void update_winding_fast()
        {
            DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
            if (spatialPro == null || spatial_timestamp != meshIn.ShapeTimestamp) {
                spatialPro = new DMeshAABBTreePro(meshIn, true);
                spatialPro.FastWindingNumber(Vector3d.Zero);
                spatial_timestamp = meshIn.ShapeTimestamp;
            }
            if (is_invalidated())
                return;

            if (cached_lazy_mwn_grid == null ||
                grid_cell_size != cached_lazy_mwn_grid.CellSize ) {

                // figure out origin & dimensions
                AxisAlignedBox3d bounds = meshIn.CachedBounds;
                float fBufferWidth = 2 * (float)grid_cell_size;
                Vector3d origin = (Vector3f)bounds.Min - fBufferWidth*Vector3f.One;
                Vector3f max = (Vector3f)bounds.Max + fBufferWidth*Vector3f.One;
                int ni = (int)((max.x - origin.x) / (float)grid_cell_size) + 1;
                int nj = (int)((max.y - origin.y) / (float)grid_cell_size) + 1;
                int nk = (int)((max.z - origin.z) / (float)grid_cell_size) + 1;

                var grid = new CachingDenseGridTrilinearImplicit(origin, grid_cell_size, new Vector3i(ni, nj, nk));
                grid.AnalyticF = new WindingField(spatialPro);

                cached_lazy_mwn_grid = grid;
                cached_mwn_bounds = meshIn.CachedBounds;
            }

            WindingFieldImplicit iso = new WindingFieldImplicit(cached_lazy_mwn_grid, winding_iso);

            MarchingCubesPro c = new MarchingCubesPro();
            c.Implicit = iso;
            c.Bounds = cached_mwn_bounds;
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(3 * c.CubeSize);
            c.RootMode = MarchingCubesPro.RootfindingModes.Bisection;
            c.RootModeSteps = 10;

            c.CancelF = is_invalidated;
            c.GenerateContinuation(meshIn.Vertices());
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

            // reproject - if we want to do this, we need to create spatial and meshIn above!
            gParallel.ForEach(c.Mesh.VertexIndices(), (vid) => {
                if (is_invalidated())
                    return;
                Vector3d v = c.Mesh.GetVertex(vid);
                int tid = spatialPro.FindNearestTriangle(v, grid_cell_size * MathUtil.SqrtTwo);
                if (tid != DMesh3.InvalidID) {
                    var query = MeshQueries.TriangleDistance(meshIn, tid, v);
                    if (v.Distance(query.TriangleClosest) < grid_cell_size)
                        c.Mesh.SetVertex(vid, query.TriangleClosest);
                }
            });

            if (is_invalidated())
                return;

            ResultMesh = c.Mesh;
        }






        /// <summary>
        /// Directly evaluate the winding number inside the marching cubes,
        /// instead of sampling onto a grid. More precise, basically.
        /// </summary>
        protected virtual void update_winding_exact()
        {
            DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
            if (spatialPro == null || spatial_timestamp != meshIn.ShapeTimestamp) {
                spatialPro = new DMeshAABBTreePro(meshIn, true);
                spatialPro.FastWindingNumber(Vector3d.Zero);
                spatial_timestamp = meshIn.ShapeTimestamp;
            }
            if (is_invalidated())
                return;

            MarchingCubesPro c = new MarchingCubesPro();
            c.Implicit = new WindingNumberImplicit(spatialPro, winding_iso);
            c.IsoValue = 0.0;
            c.Bounds = meshIn.CachedBounds;
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(3 * c.CubeSize);
            c.RootMode = MarchingCubesPro.RootfindingModes.Bisection;
            c.RootModeSteps = 5;

            c.CancelF = is_invalidated;
            c.GenerateContinuation(meshIn.Vertices());
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

            if (is_invalidated())
                return;

            ResultMesh = c.Mesh;
        }








        // level-set cache info
        double cached_sdf_max_offset = 0;
        MeshSignedDistanceGrid cached_sdf;
        AxisAlignedBox3d cached_sdf_bounds;


        protected virtual void update_level_set()
        {
            double unsigned_offset = Math.Abs(offset_distance);
            if (cached_sdf == null ||
                unsigned_offset > cached_sdf_max_offset ||
                grid_cell_size != cached_sdf.CellSize) {
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
                    return;
                cached_sdf = sdf;
                cached_sdf_max_offset = unsigned_offset;
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



        // [TODO] should be elsewhere...
        public class SampledGridImplicit : ImplicitFunction3d
        {
            DenseGridTrilinearImplicit WindingGrid;
            float IsoValue;

            public SampledGridImplicit(MeshScalarSamplingGrid scalarGrid)
            {
                Initialize(scalarGrid);
            }

            public void Initialize(MeshScalarSamplingGrid scalarGrid)
            {
                WindingGrid = new DenseGridTrilinearImplicit(scalarGrid.Grid, scalarGrid.GridOrigin, scalarGrid.CellSize);
                IsoValue = scalarGrid.IsoValue;
            }

            public double Value(ref Vector3d pt)
            {
                double winding = WindingGrid.Value(ref pt);
                if (winding == WindingGrid.Outside)
                    winding = 0;

                // shift zero-isocontour to winding isovalue, and then flip sign
                return -(winding - IsoValue);

            }
        }



        // [TODO] should be elsewhere...
        public class WindingField: ImplicitFunction3d
        {
            public DMeshAABBTreePro Spatial;
            public WindingField(DMeshAABBTreePro spatial) {
                Spatial = spatial;
            }

            public double Value(ref Vector3d pt) {
                return Spatial.FastWindingNumber(pt);
            }
        }


        public class WindingFieldImplicit : ImplicitFunction3d
        {
            public ImplicitFunction3d WindingF;
            public double IsoValue;

            public WindingFieldImplicit(ImplicitFunction3d wf, double isoValue) {
                WindingF = wf;
                IsoValue = isoValue;
            }

            public double Value(ref Vector3d pt) {
                double winding = WindingF.Value(ref pt);
                // shift zero-isocontour to winding isovalue, and then flip sign
                return -(winding - IsoValue);
            }
        }


        // [TODO] should be elsewhere...
        public class WindingNumberImplicit : ImplicitFunction3d
        {
            public DMeshAABBTreePro Spatial;
            public double IsoValue;

            public WindingNumberImplicit(DMeshAABBTreePro spatial, double isoValue)
            {
                Spatial = spatial;
                IsoValue = isoValue;
            }

            public double Value(ref Vector3d pt)
            {
                double winding = Spatial.FastWindingNumber(pt);

                // shift zero-isocontour to winding isovalue, and then flip sign
                return -(winding - IsoValue);
            }
        }





        public static DMeshAABBTree3 MeshSDFShouldUseSpatial(DMeshAABBTree3 treeIn, int exact_cells, double cell_size, double avg_edge_len)
        {
            double metric = cell_size / avg_edge_len;
            double w_metric = exact_cells * metric;
            if (exact_cells < 2 || w_metric < 0.25)
                return null;
            return treeIn;
        }

    }


}
