using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace OnsrudFocasService
{
    public class FOCASParseData
    {
        private DateTime _feedHoldStart = DateTime.MinValue;

        public TimeSpan FeedHoldTime(int signal, TimeSpan currentFeedHold)
        {
            DateTime currentStartTime = DateTime.Now;

            if (signal == 0 && _feedHoldStart == DateTime.MinValue)
            {
                _feedHoldStart = DateTime.Now;
            }
            else if (signal == 1 && _feedHoldStart != DateTime.MinValue)
            {
                currentStartTime = _feedHoldStart;
                _feedHoldStart = DateTime.MinValue; 
            }

            return currentFeedHold.Add(DateTime.Now.Subtract(currentStartTime));
        }

        public static int[] ConvertToBitArray(int value, int start, int end)
        {
            if (end < start)
                throw new ArgumentException("Start cannot be greater than end");

            int[] tempArray = new int[end - start + 1];

            string s = Convert.ToString(value, 2);

            int[] bits = s.PadLeft(end - start, '0').Select(c => int.Parse(c.ToString())).ToArray();

            Array.Reverse(bits);

            return bits;
        }

        public static int FlipBit(int currentValue, int bitToFlip, bool onOrOff)
        {
            if (bitToFlip > 15 || bitToFlip < 0)
                throw new ArgumentOutOfRangeException("An integer contains 32 bits. Cannot change bit: " + bitToFlip);

            BitArray b = new BitArray(new int[] { currentValue });

            b.Set(bitToFlip, onOrOff);

            int[] arr = new int[1];

            b.CopyTo(arr, 0);

            return arr[0]; 
        }

        public static short FlipBit(short value, int bitToFlip, bool onOrOff)
        {
            if (bitToFlip > 15 || bitToFlip < 0)
                throw new ArgumentOutOfRangeException("A short contains 16 bits. Cannot change bit: " + bitToFlip);

            BitArray b = new BitArray(new int[] { value });

            b.Set(bitToFlip, onOrOff);

            int[] arr = new int[1];

            b.CopyTo(arr, 0);

            short retVal = Convert.ToInt16(arr[0]);

            return retVal;
        }
    }

    public static class Extensions
    {
        public static bool ActionComparer(this Action firstAction, Action secondAction)
        {
            if (firstAction.Target != secondAction.Target)
                return false;

            var firstMethodBody = firstAction.Method.GetMethodBody().GetILAsByteArray();
            var secondMethodBody = secondAction.Method.GetMethodBody().GetILAsByteArray();

            if (firstMethodBody.Length != secondMethodBody.Length)
                return false;

            for (var i = 0; i < firstMethodBody.Length; i++)
            {
                if (firstMethodBody[i] != secondMethodBody[i])
                    return false;
            }
            return true;
        }

        public static int Replace<T>(this IList<T> source, T oldValue, T newValue)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            var index = source.IndexOf(oldValue);
            if (index != -1)
                source[index] = newValue;
            return index;
        }

        public static bool GetBit(this byte b, int bitNumber)
        {
            return (b & (1 << bitNumber)) != 0;
        }

        public static bool GetBit(this int b, int bitNumber)
        {
            return (b & (1 << bitNumber)) != 0;
        }

        public static string GetDrivesReadyString(this int ready)
        {
            if (ready == 1)
                return "Drives On";
            else
                return "In Estop";
        }

        public static T[] FocasClassToArray<T, T2>(this T2 cls, T returnType)
        {
            var properties = typeof(T2).GetFields();
            T[] arr = new T[properties.Length];

            short index = 0;

            foreach (var property in properties)
            {
                if (returnType.GetType() != property.FieldType)
                    throw new ArgumentException("Cannot convert from " + property.FieldType + " to " + returnType.GetType() + ". Please supply the fuction with an instance of " + property.FieldType);

                dynamic data = property.GetValue(cls);

                arr[index] = data;

                index++;
            }

            return arr;
        }
    }
}
