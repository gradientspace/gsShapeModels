// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using g3;

namespace gs
{
    public class ThreadedMeshComputeOp : BaseModelingOperator, DMeshComputeOp
    {
        DMeshSourceOp mesh_source;
        public DMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null) {
                    mesh_source.OperatorModified -= on_source_modified;
                    mesh_source.OperatorException -= on_source_exception;
                }
                mesh_source = value;
                if (mesh_source != null) {
                    mesh_source.OperatorModified += on_source_modified;
                    mesh_source.OperatorException += on_source_exception;
                }
                invalidate();
            }
        }


        bool result_valid = false;
        int result_timestamp = 1;
        int last_exception_timestamp = 0;

        object compute_thread_lock;
        bool computing = false;
        DMesh3 computed_result;

        public ThreadedMeshComputeOp()
        {
            compute_thread_lock = new object();
        }

        protected virtual void on_source_modified(ModelingOperator op)
        {
            invalidate();
            PostOnOperatorModified();
        }



        void invalidate()
        {
            result_valid = false;
            result_timestamp++;

            // [RMS] disabled this. It causes too many problems to immediately spawn the compute
            //   on invalidate(). We want the result to be "pulled", not to "push" it.
            // [TODO] maybe this should be an option?

            // kick off new compute, if we are not already computing (this fn will check)
            //spawn_recompute();
        }


        class thread_timestamp
        {
            public int timestamp;
        }
        thread_timestamp active_thread_data = null;


        void spawn_recompute()
        {
            Thread computer = null;
            lock (compute_thread_lock) {
                if (computing == false) {
                    computing = true;
                    active_thread_data = new thread_timestamp() { timestamp = this.result_timestamp };
                    ActiveComputeExceptions = new List<ModelingOpException>();      // maybe should go in active_thread_data ?
                    computer = new Thread(ComputeThreadFunc);
                }
            }

            if (computer != null) {
                computer.Start(active_thread_data);
            }
        }


        public bool IsComputing {
            get { return computing; }
        }
        public bool IsResultAvailable {
            get { return computing == false && result_valid == false; }
        }
        public bool ResultConsumed {
            get { return computing == false && result_valid == true; }
        }



        List<ModelingOpException> ActiveComputeExceptions;
        List<ModelingOpException> LastComputeExceptions;


        protected void on_source_exception(ModelingOpException mopex)
        {
            ActiveComputeExceptions.Add(mopex);
        }


        public DMeshOutputStatus CheckForNewMesh()
        {
            if (computing) {
                return DMeshOutputStatus.Computing();

            } else if (result_valid) {
                return DMeshOutputStatus.Unavailable();

            } else if (last_exception_timestamp == result_timestamp) {
                return DMeshOutputStatus.Unavailable();

            } else {
                // the compute we were waiting for has finished, extract the resulting mesh

                DMesh3 returnMesh = null;
                bool spawn_new_compute = false;

                lock (compute_thread_lock) {
                    returnMesh = computed_result;
                    computed_result = null;

                    // situations where we will discard this result:
                    //  1) it came back null. lets hope this is temporary...
                    //  2) active_thread_data was null. This means we hadn't actually spawned
                    //     the compute thread yet. [TODO] maybe this should happen another way?
                    //  3) the timestamp increment since we spawned the current comput thread.
                    //     Discard the result and spawn a new compute.
                    bool use_result = (returnMesh != null) &&
                        (active_thread_data != null) &&
                        (active_thread_data.timestamp == result_timestamp);

                    if ( use_result ) {
                        result_valid = true;
                    } else {
                        result_valid = false;
                        spawn_new_compute = true;
                    }

                    active_thread_data = null;

                    LastComputeExceptions = ActiveComputeExceptions;
                    ActiveComputeExceptions = null;
                }

                if ( spawn_new_compute )
                    spawn_recompute();

                if (returnMesh == null) {
                    return (computing) ? DMeshOutputStatus.Computing() : DMeshOutputStatus.Unavailable();
                } else {
                    return DMeshOutputStatus.Ready(returnMesh, LastComputeExceptions);
                }
            }
        }




        void ComputeThreadFunc(object thread_data)
        {
            thread_timestamp data = thread_data as thread_timestamp;

            try {
                DMesh3 result = MeshSource.ExtractDMesh();
                lock (compute_thread_lock) {
                    computed_result = result;
                    computing = false;
                }
            } catch(Exception e) {
                background_exception = e;
                computing = false;
                last_exception_timestamp = data.timestamp;
            }
        }



        Exception background_exception = null;
        public bool HaveBackgroundException {
            get { return background_exception != null; }
        }
        public Exception ExtractBackgroundException()
        {
            var r = background_exception;
            background_exception = null;
            return r;
        }
    }
}
