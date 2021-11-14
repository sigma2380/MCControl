using System;

namespace MCControl
{
   public class TimerTracker<T>
   {
      private T myCurrenValue;
      private DateTime lastUpdate = new DateTime(2015, 12, 31);

      public T Value
      {
         get
         {
            return myCurrenValue;
         }

         set
         {
            myCurrenValue = value;
            lastUpdate = DateTime.Now;
         }
      }

      public TimeSpan Timeout
      {
         get; set;
      }

      public bool IsStale => DateTime.Now - lastUpdate > Timeout;
   }
}
