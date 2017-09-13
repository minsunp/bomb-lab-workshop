/* Ben Scott * @evan-erdos * bescott@andrew.cmu.edu * 2016-11-13 */

using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DateTime=System.DateTime;
using Type=System.Type;
using YamlDotNet.Serialization;
using UnityEngine;

/// YamlReader : coroutine
/// Waits for a WWW instance to either download or fail,
/// and then reads in the data as *.yml and deserializes.
/// - path : string
///     unescaped URL to use
/// - func : action
///     invoked after www is done downloading
public abstract class YamlReader : CustomYieldInstruction {
    public override bool keepWaiting => false;
    public static string prefix {get;} = "tag:yaml.org,2002:";
    public static List<IYamlTypeConverter> converters {get;} =
        new List<IYamlTypeConverter> {
            new RegexYamlConverter(),
            new Vector2YamlConverter()};

    public static Dictionary<string,Type> tags {get;} =
        new Dictionary<string,Type> {
            ["regex"] = typeof(Regex),
            ["date"] = typeof(DateTime)};


    public static Deserializer GetDefaultDeserializer() {
        var obj = new Deserializer(
            namingConvention: new SemanticNamingConvention(),
            ignoreUnmatched: true);
        converters.ForEach(o => obj.RegisterTypeConverter(o));
        foreach (var o in tags) obj.RegisterTagMapping($"{prefix}{o.Key}",o.Value);
        return obj;
    }

    public static Type GetMap(string o) => GetMap(tags[o]);
    public static Type GetMap(Type o) => typeof(Dictionary<,>).MakeGenericType(o);
    public static string FromType(Type o) =>
        new Regex(@"\+Data").Replace(o.FullName.Split('.').Last(),"").ToLower();
}

public class YamlReader<T> : YamlReader {
    Deserializer reader = new Deserializer();
    Action<T> func;
    WWW www;

    public override bool keepWaiting { get {
        if (!www.isDone) return true;
        if (string.IsNullOrEmpty(www.error)) Read(www.text);
        return false; } }

    public YamlReader(string path, Action<T> func) {
        converters.ForEach(o => reader.RegisterTypeConverter(o));
        foreach (var o in tags)
            reader.RegisterTagMapping($"{prefix}{o.Key}", o.Value);
        www = new WWW(System.Uri.EscapeUriString(path));
        this.func = func;
    }

    void Read(string o) => func(reader.Deserialize<T>(new StringReader(o)));
}


public class SemanticNamingConvention : INamingConvention {
    public string Apply(string o) => string.Join(
        " ", Regex.Split(o,@"(?<!^)(?=[A-Z])")).ToLower().Trim(); }
