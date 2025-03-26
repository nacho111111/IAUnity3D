using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimerEvent
{
    public float triggerTime; // El tiempo en que se debe ejecutar el evento
    public Action onTimerEvent; // La acción a ejecutar cuando se cumpla el tiempo

    public TimerEvent(float time, Action action)
    {
        triggerTime = time;
        onTimerEvent = action;
    }
}
public class TimerManager : MonoBehaviour
{
    private List<TimerEvent> timerEvents = new List<TimerEvent>();

    private void Update()
    {
        float currentTime = Time.time; // Tiempo global actual
        for (int i = timerEvents.Count - 1; i >= 0; i--)
        {
            if (currentTime >= timerEvents[i].triggerTime)
            {
                // Ejecuta la acción y remueve el evento de la lista
                timerEvents[i].onTimerEvent.Invoke();
                timerEvents.RemoveAt(i);
            }
        }

    }

    public void RegisterTimerEvent(float timeInSeconds, Action action)
    {
        float triggerTime = Time.time + timeInSeconds; // Define cuándo ejecutar el evento
        timerEvents.Add(new TimerEvent(triggerTime, action));
    }
    public void ClearAllTimerEvents()
    {
        timerEvents.Clear();
    }
}
