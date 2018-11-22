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
    public class MeshVoxelBooleanOp : BaseDMeshSourceOp
    {
        public enum OpTypes
        {
            Union = 0, Intersection = 1, Difference = 2
        }
        OpTypes op_type = OpTypes.Union;
        public OpTypes OpType {
            get { return op_type; }
            set { if (op_type != value) { op_type = value; invalidate(); } }
        }





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


        double min_component_volume = 1.0;
        public double MinComponentVolume {
            get { return min_component_volume; }
            set {
                double set_value = MathUtil.Clamp(value, 0.0, 1000);
                if (Math.Abs(min_component_volume - set_value) > MathUtil.ZeroTolerancef) { min_component_volume = set_value; invalidate(); }
            }
        }


        List<DMeshSourceOp> mesh_sources = null;
        public void SetSources(List<DMeshSourceOp> sources)
        {
            if (mesh_sources != null)
                throw new Exception("todo: handle changing sources!");
            mesh_sources = new List<DMeshSourceOp>(sources);
            foreach (var source in mesh_sources)
                source.OperatorModified += on_input_modified;

            cached_sdfs = new MeshSignedDistanceGrid[sources.Count];
            cached_isos = new BoundedImplicitFunction3d[sources.Count];

            invalidate();
        }


        protected virtual void on_input_modified(ModelingOperator op)
        {
            base.invalidate();
        }

        protected virtual void invalidate_caches()
        {
            for (int k = 0; k < cached_sdfs.Length; ++k)
                cached_sdfs[k] = null;
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
                ResultMesh = compute_wrap();

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

        void cache_input_sdfs()
        {
            gParallel.ForEach(Interval1i.Range(mesh_sources.Count), (k) => {
                if (cached_sdfs[k] != null)
                    return;

                DMesh3 source_mesh = mesh_sources[k].GetDMeshUnsafe();
                int exact_cells = 1;
                MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(source_mesh, grid_cell_size) {
                    ExactBandWidth = exact_cells
                };
                sdf.CancelF = is_invalidated;
                sdf.Compute();
                if (is_invalidated())
                    return;

                cached_sdfs[k] = sdf;
                cached_isos[k] = new DenseGridTrilinearImplicit(sdf.Grid, sdf.GridOrigin, sdf.CellSize);
            });
        }



        protected virtual DMesh3 compute_wrap()
        {
            cache_input_sdfs();
            if (is_invalidated())
                return null;

            BoundedImplicitFunction3d iso = null;
            if ( op_type == OpTypes.Union ) {
                iso = new ImplicitNaryUnion3d() {
                    Children = new List<BoundedImplicitFunction3d>(cached_isos)
                };
            } else if (op_type == OpTypes.Intersection) {
                iso = new ImplicitNaryIntersection3d() {
                    Children = new List<BoundedImplicitFunction3d>(cached_isos)
                };
            } else if (op_type == OpTypes.Difference) {
                iso = new ImplicitNaryDifference3d() {
                    A = cached_isos[0],
                    BSet = new List<BoundedImplicitFunction3d>(cached_isos.Skip(1))
                };
            }

            MarchingCubes c = new MarchingCubes();
            c.Implicit = iso;
            c.IsoValue = 0;
            c.Bounds = iso.Bounds();
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
