using System;
using System.Collections.Generic;
using UnityEngine;

public static class ServiceLocator {
    private static readonly Dictionary<Type, object> _services = new();

    public static void Register<T>(T service) {
        var type = typeof(T);
        if (_services.ContainsKey(type)) {
            Debug.LogWarning($"Service of type {type} is already registered.");
            return;
        }
        _services[type] = service;
    }

    public static T Get<T>() {
        var type = typeof(T);
        if (_services.TryGetValue(type, out var service)) {
            return (T)service;
        }
        throw new InvalidOperationException($"Service of type {type} not found.");
    }

    public static bool TryGet<T>(out T service) {
        var type = typeof(T);
        if (_services.TryGetValue(type, out var obj)) {
            service = (T)obj;
            return true;
        }
        service = default;
        return false;
    }

    public static void Unregister<T>() {
        var type = typeof(T);
        if (!_services.Remove(type)) {
            Debug.LogWarning($"Service of type {type} was not registered.");
        }
    }

    public static void Clear() {
        _services.Clear();
    }
}
