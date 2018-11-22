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
    public class MeshVoxelBlendOp : BaseDMeshSourceOp
    {
        double grid_cell_size = 1.0;
        public double GridCellSize {
            get { return grid_cell_size; }
            set {
                double set_size = MathUtil.Clamp(value, 0.0001, 10000.0);
                if (Math.Abs(grid_cell_size - set_size) > MathUtil.ZeroTolerancef) { grid_cell_size = value; invalidate_caches(); }
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

        double blend_power = 1.0;
        public double BlendPower {
            get { return blend_power; }
            set {
                double set_size = MathUtil.Clamp(value, 0.001, 10000.0);
                if (Math.Abs(blend_power - set_size) > MathUtil.ZeroTolerancef) { blend_power = value; invalidate(); }
            }
        }

        double blend_falloff = 5.0;
        public double BlendFalloff {
            get { return blend_falloff; }
            set {
                double set_value = MathUtil.Clamp(value, 0.001, 10000.0);
                if (Math.Abs(blend_power - set_value) > MathUtil.ZeroTolerancef) { blend_falloff = value; invalidate(); }
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



        List<DMeshSourceOp> mesh_sources = null;
        List<Vector3d> source_edge_stats = null;
        AxisAlignedBox3d source_bounds;

        public void SetSources(List<DMeshSourceOp> sources)
        {
            if (mesh_sources != null)
                throw new Exception("MeshVoxelBlendOp.SetSources: handle changing sources!");
            //if (sources.Count != 2)
            //    throw new Exception("MeshVoxelBlendOp.SetSources: only two sources supported!");
            mesh_sources = new List<DMeshSourceOp>(sources);
            foreach (var source in mesh_sources)
                source.OperatorModified += on_input_modified;

            cached_sdfs = new MeshSignedDistanceGrid[sources.Count];
            cached_isos = new BoundedImplicitFunction3d[sources.Count];
            cached_bvtrees = new DMeshAABBTreePro[sources.Count];

            invalidate();
        }


        protected virtual void on_input_modified(ModelingOperator op)
        {
            base.invalidate();
        }

        protected virtual void invalidate_caches()
        {
            for (int k = 0; k < cached_sdfs.Length; ++k) {
                cached_sdfs[k] = null;
                cached_isos = null;
            }
            cached_bounded_sdfs = null;
            cached_lazy_sdfs = null;
            base.invalidate();
        }


        DMesh3 ResultMesh;


        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (mesh_sources == null)
                throw new Exception("MeshVoxelBooleanOp: must set valid MeshSource to compute!");

            try {
                ResultMesh = null;

                if (source_edge_stats == null) {
                    source_edge_stats = new List<Vector3d>();
                    source_bounds = AxisAlignedBox3d.Empty;
                    foreach (DMeshSourceOp op in mesh_sources) {
                        Vector3d einfo = new Vector3d();
                        MeshQueries.EdgeLengthStats(op.GetDMeshUnsafe(), out einfo.x, out einfo.y, out einfo.z);
                        source_edge_stats.Add(einfo);
                        source_bounds.Contain(op.GetDMeshUnsafe().CachedBounds);
                    }
                }

                //ResultMesh = compute_blend();
                //ResultMesh = compute_blend_bounded();
                ResultMesh = compute_blend_analytic();

                if (ResultMesh.TriangleCount == 0)
                    ResultMesh = base.make_failure_output(null);

                base.complete_update();

            } catch (Exception e) {
                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(mesh_sources[0].GetDMeshUnsafe());
                base.complete_update();
            }

        }


        MeshSignedDistanceGrid[] cached_sdfs;
        BoundedImplicitFunction3d[] cached_isos;
        DMeshAABBTreePro[] cached_bvtrees;

        void cache_input_sdfs()
        {
            gParallel.ForEach(Interval1i.Range(mesh_sources.Count), (k) => {
                if (cached_sdfs[k] != null)
                    return;
                if (is_invalidated())
                    return;

                DMesh3 source_mesh = mesh_sources[k].GetDMeshUnsafe();
                Vector3d expand = source_mesh.CachedBounds.Extents;

                int exact_cells = 2;
                MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(source_mesh, grid_cell_size) {
                    ExactBandWidth = exact_cells,
                    ComputeMode = MeshSignedDistanceGrid.ComputeModes.FullGrid,
                    ExpandBounds = expand
                };
                sdf.CancelF = is_invalidated;
                sdf.Compute();
                if (is_invalidated())
                    return;


                cached_sdfs[k] = sdf;
                cached_isos[k] = new DenseGridTrilinearImplicit(sdf.Grid, sdf.GridOrigin, sdf.CellSize);
            });
        }





        protected virtual DMesh3 compute_blend()
        {
            cache_input_sdfs();
            if (is_invalidated())
                return null;

            ImplicitBlend3d blend = new ImplicitBlend3d() {
                A = cached_isos[0],
                B = cached_isos[1],
                Blend = blend_power,
                WeightA = 1.0,
                WeightB = 1.0
            };

            MarchingCubes c = new MarchingCubes();
            c.Implicit = blend;
            c.IsoValue = 0;
            c.Bounds = blend.Bounds();
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(3 * c.CubeSize);
            c.RootMode = MarchingCubes.RootfindingModes.Bisection;
            c.RootModeSteps = 5;

            c.CancelF = is_invalidated;
            c.Generate();
            if (is_invalidated())
                return null;

            Reducer r = new Reducer(c.Mesh);
            r.FastCollapsePass(c.CubeSize/2, 3, true);
            if (is_invalidated())
                return null;

            if (min_component_volume > 0)
                MeshEditor.RemoveSmallComponents(c.Mesh, min_component_volume, min_component_volume);
            if (is_invalidated())
                return null;

            return c.Mesh;
        }





        MeshSignedDistanceGrid[] cached_bounded_sdfs;
        double[] cached_bounded_sdf_maxdist;





        void cache_input_sdfs_bounded()
        {
            if (cached_bounded_sdfs == null) {
                cached_bounded_sdfs = new MeshSignedDistanceGrid[mesh_sources.Count];
                cached_bounded_sdf_maxdist = new double[mesh_sources.Count];
            }
            cache_bvtrees(false);

            double falloff_distance = blend_falloff;

            gParallel.ForEach(Interval1i.Range(mesh_sources.Count), (k) => {
                if (falloff_distance > cached_bounded_sdf_maxdist[k])
                    cached_bounded_sdfs[k] = null;

                // [TODO] we could expand via flood-fill here instead of throwing away all previously computed!

                if (cached_bounded_sdfs[k] != null)
                    return;
                if (is_invalidated())
                    return;

                int exact_cells = (int)(falloff_distance / grid_cell_size) + 2;

                DMesh3 source_mesh = mesh_sources[k].GetDMeshUnsafe();
                DMeshAABBTree3 use_spatial = GenerateClosedMeshOp.MeshSDFShouldUseSpatial(
                    cached_bvtrees[k], exact_cells, grid_cell_size, source_edge_stats[k].z);

                MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(source_mesh, grid_cell_size, use_spatial) {
                    ExactBandWidth = exact_cells
                };
                if (use_spatial != null) {
                    sdf.NarrowBandMaxDistance = falloff_distance + grid_cell_size;
                    sdf.ComputeMode = MeshSignedDistanceGrid.ComputeModes.NarrowBand_SpatialFloodFill;
                }
                sdf.CancelF = is_invalidated;
                sdf.Compute();
                if (is_invalidated())
                    return;

                cached_bounded_sdfs[k] = sdf;
                cached_bounded_sdf_maxdist[k] = falloff_distance;
            });
        }





        protected virtual DMesh3 compute_blend_bounded()
        {
            bool profile = true;
            LocalProfiler p = null;
            if (profile) {
                p = new LocalProfiler();
                p.Start("sdf");
            }

            cache_input_sdfs_bounded();
            if (is_invalidated())
                return null;

            if (profile) {
                p.Stop("sdf");
                p.Start("mc");
            }

            List<BoundedImplicitFunction3d> inputs = new List<BoundedImplicitFunction3d>();
            foreach ( var sdf in cached_bounded_sdfs ) {
                var dist_field = new DenseGridTrilinearImplicit(sdf);
                var skel_field = new DistanceFieldToSkeletalField() { DistanceField = dist_field, FalloffDistance = blend_falloff };
                inputs.Add(skel_field);
            }

            SkeletalRicciNaryBlend3d blend = new SkeletalRicciNaryBlend3d() {
                Children = inputs, BlendPower = this.blend_power
            };

            MarchingCubesPro c = new MarchingCubesPro();
            c.Implicit = blend;
            c.IsoValue = DistanceFieldToSkeletalField.ZeroIsocontour;
            c.Bounds = blend.Bounds();
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(3 * c.CubeSize);
            c.RootMode = MarchingCubesPro.RootfindingModes.LerpSteps;
            c.RootModeSteps = 3;

            c.CancelF = is_invalidated;
            //c.Generate();
            c.GenerateContinuation(input_mesh_seeds());
            if (is_invalidated())
                return null;

            if (profile) {
                p.Stop("mc");
                p.Start("reduce");
            }

            c.Mesh.ReverseOrientation();

            Reducer r = new Reducer(c.Mesh);
            r.FastCollapsePass(c.CubeSize / 2, 3, true);
            if (is_invalidated())
                return null;

            if (min_component_volume > 0)
                MeshEditor.RemoveSmallComponents(c.Mesh, min_component_volume, min_component_volume);
            if (is_invalidated())
                return null;

            if (profile) {
                p.Stop("reduce");
#if G3_USING_UNITY
                UnityEngine.Debug.Log("BLEND TIME: " + p.AllTimes());
#endif
            }

            return c.Mesh;
        }



        IEnumerable<Vector3d> input_mesh_seeds()
        {
            foreach (var source in mesh_sources) {
                DMesh3 mesh = source.GetDMeshUnsafe();
                foreach (Vector3d v in mesh.Vertices())
                    yield return v;
            }
        }






        void cache_bvtrees(bool bWinding)
        {
            gParallel.ForEach(Interval1i.Range(mesh_sources.Count), (k) => {
                if (cached_bvtrees[k] != null)
                    return;
                if (is_invalidated())
                    return;
                DMesh3 source_mesh = mesh_sources[k].GetDMeshUnsafe();
                cached_bvtrees[k] = new DMeshAABBTreePro(source_mesh, true);
            });

            if (bWinding) {
                gParallel.ForEach(Interval1i.Range(mesh_sources.Count), (k) => {
                    if (is_invalidated())
                        return;
                    cached_bvtrees[k].FastWindingNumber(Vector3d.Zero);
                });
            }
        }




        CachingMeshSDF[] cached_lazy_sdfs;
        double[] cached_lazy_sdf_maxdists;


        void compute_cache_lazy_sdfs()
        {
            if (cached_lazy_sdfs == null) {
                cached_lazy_sdfs = new CachingMeshSDF[mesh_sources.Count];
                cached_lazy_sdf_maxdists = new double[mesh_sources.Count];
            }
            cache_bvtrees(false);

            double need_distance = blend_falloff;

            gParallel.ForEach(Interval1i.Range(mesh_sources.Count), (k) => {
                if (need_distance > cached_lazy_sdf_maxdists[k])
                    cached_lazy_sdfs[k] = null;

                // [TODO] we could expand via flood-fill here instead of throwing away all previously computed!

                if (cached_lazy_sdfs[k] != null)
                    return;
                if (is_invalidated())
                    return;

                float use_max_offset = (float)blend_falloff; // (float)(3 * blend_falloff);

                DMesh3 source_mesh = mesh_sources[k].GetDMeshUnsafe();
                CachingMeshSDF sdf = new CachingMeshSDF(source_mesh, grid_cell_size, cached_bvtrees[k]) {
                    MaxOffsetDistance = use_max_offset
                };
                sdf.CancelF = is_invalidated;
                sdf.Initialize();
                if (is_invalidated())
                    return;

                cached_lazy_sdfs[k] = sdf;
                cached_lazy_sdf_maxdists[k] = use_max_offset;
            });
        }


        // continuation polygonization can re-use allocated memory
        MarchingCubesPro lazy_mc = new MarchingCubesPro();


        protected virtual DMesh3 compute_blend_analytic()
        {
            bool profile = true;
            LocalProfiler p = null;
            if (profile) {
                p = new LocalProfiler();
                p.Start("bvtree");
            }

            compute_cache_lazy_sdfs();
            if (is_invalidated())
                return null;


            if (profile) {
                p.Stop("bvtree");
                p.Start("mc");
            }

            List<BoundedImplicitFunction3d> inputs = new List<BoundedImplicitFunction3d>();
            foreach (CachingMeshSDF sdf in cached_lazy_sdfs) { 
                var skel_field = new DistanceFieldToSkeletalField() {
                    DistanceField = new CachingMeshSDFImplicit(sdf), FalloffDistance = blend_falloff };
                inputs.Add(skel_field);
            }

            SkeletalRicciNaryBlend3d blend = new SkeletalRicciNaryBlend3d() {
                Children = inputs, BlendPower = this.blend_power,
                FieldShift = -DistanceFieldToSkeletalField.ZeroIsocontour
            };

            AxisAlignedBox3d use_bounds = source_bounds;
            source_bounds.Expand(blend_falloff);

            MarchingCubesPro c = lazy_mc;
            c.Implicit = blend;
            //c.IsoValue = DistanceFieldToSkeletalField.ZeroIsocontour;
            c.Bounds = use_bounds;
            c.CubeSize = mesh_cell_size;
            c.Bounds.Expand(3 * c.CubeSize);
            c.RootMode = MarchingCubesPro.RootfindingModes.LerpSteps;
            c.RootModeSteps = 3;
            //c.ParallelCompute = false;

            c.CancelF = is_invalidated;
            c.GenerateContinuation(input_mesh_seeds());
            if (is_invalidated())
                return null;

            if (profile) {
                p.Stop("mc");
                p.Start("reduce");
            }

            c.Mesh.ReverseOrientation();

            Reducer r = new Reducer(c.Mesh);
            r.FastCollapsePass(c.CubeSize / 2, 3, true);
            if (is_invalidated())
                return null;

            if (min_component_volume > 0)
                MeshEditor.RemoveSmallComponents(c.Mesh, min_component_volume, min_component_volume);
            if (is_invalidated())
                return null;

            if (profile) {
                p.Stop("reduce");
#if G3_USING_UNITY
                UnityEngine.Debug.Log("ANALYTIC BLEND TIME: " + p.AllTimes());
#endif
            }

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
