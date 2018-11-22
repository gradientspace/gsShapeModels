// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using g3;

namespace gs
{
    public class ThreadedResultComputeOp<T> : BaseModelingOperator, ResultComputeOp<T>
    {
        ResultSourceOp<T> result_source;
        public ResultSourceOp<T> ResultSource {
            get { return result_source; }
            set {
                if (result_source != null) {
                    result_source.OperatorModified -= on_source_modified;
                    result_source.OperatorException -= on_source_exception;
                }
                result_source = value;
                if (result_source != null) {
                    result_source.OperatorModified += on_source_modified;
                    result_source.OperatorException += on_source_exception;
                }
                invalidate();
            }
        }


        bool result_valid = false;
        int result_timestamp = 1;
        int last_exception_timestamp = 0;

        object compute_thread_lock;
        bool computing = false;
        T computed_result;

        public ThreadedResultComputeOp()
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



        public OpResultOutputStatus<T> CheckForNewResult()
        {
            if (computing) {
                return OpResultOutputStatus<T>.Computing();

            } else if (result_valid) {
                return OpResultOutputStatus<T>.Unavailable();

            } else if (last_exception_timestamp == result_timestamp) {
                return OpResultOutputStatus<T>.Unavailable();

            } else {
                // the compute we were waiting for has finished, extract the resulting mesh

                T returnResult = default(T);
                bool spawn_new_compute = false;

                lock (compute_thread_lock) {
                    returnResult = computed_result;
                    computed_result = default(T);

                    // situations where we will discard this result:
                    //  1) it came back null. lets hope this is temporary...
                    //  2) active_thread_data was null. This means we hadn't actually spawned
                    //     the compute thread yet. [TODO] maybe this should happen another way?
                    //  3) the timestamp increment since we spawned the current comput thread.
                    //     Discard the result and spawn a new compute.
                    bool use_result = (returnResult != null) &&
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

                if (returnResult == null) {
                    return (computing) ? OpResultOutputStatus<T>.Computing() : OpResultOutputStatus<T>.Unavailable();
                } else {
                    return OpResultOutputStatus<T>.Ready(returnResult, LastComputeExceptions);
                }
            }
        }




        void ComputeThreadFunc(object thread_data)
        {
            thread_timestamp data = thread_data as thread_timestamp;

            try {
                T result = ResultSource.ExtractResult();
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
