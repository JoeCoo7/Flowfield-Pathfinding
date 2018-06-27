using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;

struct BurstQueue
{
    public NativeArray<int> array;

    int start;
    int end;
    int length;
    public int Length { get { return length; } }

    public BurstQueue(NativeArray<int> queueArray)
    {
        array = queueArray;
        start = 0;
        end = 0;
        length = 0;
    }

    public void Enqueue(int value)
    {
        array[end] = value;
        end = (end + 1) % array.Length;
        ++length;
    }

    public int Dequeue()
    {
        var retVal = array[start];
        start = (start + 1) % array.Length;
        --length;
        return retVal;
    }
}
