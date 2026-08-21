using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitBridge
{
    public enum TaskType
    {
        HealthCheck,
        GetSelection,
        SelectElements,
        GetElementParameters,
        GetElementInfo,
        GetElementsByCategory,
        SetParameter,
        GetProjectInfo,
        GetLevels,
        GetViews,
        DeleteElements
    }

    public class BridgeTask
    {
        public TaskType Type { get; set; }
        public string? PayloadJson { get; set; }
        public TaskCompletionSource<string> TaskCompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public class RevitExternalHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<BridgeTask> _taskQueue = new();

        public void EnqueueTask(BridgeTask task)
        {
            _taskQueue.Enqueue(task);
        }

        public void Execute(UIApplication app)
        {
            while (_taskQueue.TryDequeue(out var task))
            {
                try
                {
                    switch (task.Type)
                    {
                        case TaskType.HealthCheck:
                            HandleHealthCheck(app, task);
                            break;

                        case TaskType.GetSelection:
                            HandleGetSelection(app, task);
                            break;

                        case TaskType.SelectElements:
                            HandleSelectElements(app, task);
                            break;

                        case TaskType.GetElementParameters:
                            HandleGetElementParameters(app, task);
                            break;

                        case TaskType.GetElementInfo:
                            HandleGetElementInfo(app, task);
                            break;

                        case TaskType.GetElementsByCategory:
                            HandleGetElementsByCategory(app, task);
                            break;

                        case TaskType.SetParameter:
                            HandleSetParameter(app, task);
                            break;

                        case TaskType.GetProjectInfo:
                            HandleGetProjectInfo(app, task);
                            break;

                        case TaskType.GetLevels:
                            HandleGetLevels(app, task);
                            break;

                        case TaskType.GetViews:
                            HandleGetViews(app, task);
                            break;

                        case TaskType.DeleteElements:
                            HandleDeleteElements(app, task);
                            break;

                        default:
                            task.TaskCompletionSource.SetResult(CreateErrorJson("Unknown task type."));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson($"Revit API execution failed: {ex.Message}"));
                }
            }
        }

        private static void HandleHealthCheck(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            var doc = uiDoc?.Document;

            var responseData = new
            {
                status = "online",
                service = "RevitBridge",
                revit_version = app.Application.VersionNumber,
                revit_build = app.Application.VersionBuild,
                active_document = doc != null ? doc.Title : "No active document"
            };

            task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(responseData));
        }

        private static void HandleGetSelection(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            var doc = uiDoc.Document;
            var selectionIds = uiDoc.Selection.GetElementIds();
            var elementsList = new List<object>();

            foreach (var id in selectionIds)
            {
                var element = doc.GetElement(id);
                if (element == null) continue;

                elementsList.Add(new
                {
                    element_id = id.Value,
                    category = element.Category?.Name ?? "Uncategorized",
                    name = element.Name ?? "Unnamed"
                });
            }

            var resultJson = JsonSerializer.Serialize(new
            {
                status = "success",
                count = elementsList.Count,
                selection = elementsList
            });

            task.TaskCompletionSource.SetResult(resultJson);
        }

        private static void HandleSelectElements(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            if (string.IsNullOrWhiteSpace(task.PayloadJson))
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("Payload JSON is empty."));
                return;
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(task.PayloadJson);
                if (!jsonDoc.RootElement.TryGetProperty("element_ids", out var idsProp) || idsProp.ValueKind != JsonValueKind.Array)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("Property 'element_ids' must be an array of integers."));
                    return;
                }

                var idsList = new List<ElementId>();
                foreach (var item in idsProp.EnumerateArray())
                {
                    if (item.TryGetInt64(out long idVal))
                    {
                        idsList.Add(new ElementId(idVal));
                    }
                }

                uiDoc.Selection.SetElementIds(idsList);

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    message = $"{idsList.Count} element(s) selected in Revit UI.",
                    selected_count = idsList.Count
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to select elements: {ex.Message}"));
            }
        }

        private static void HandleGetElementParameters(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            if (string.IsNullOrWhiteSpace(task.PayloadJson))
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("Payload JSON is empty."));
                return;
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(task.PayloadJson);
                if (!jsonDoc.RootElement.TryGetProperty("element_id", out var idProp))
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("Property 'element_id' is required."));
                    return;
                }

                long elementId = idProp.GetInt64();
                var doc = uiDoc.Document;
                var elem = doc.GetElement(new ElementId(elementId));

                if (elem == null)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson($"Element ID {elementId} not found."));
                    return;
                }

                var instanceParams = ExtractParameters(elem);

                List<object>? typeParams = null;
                var typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    var elemType = doc.GetElement(typeId);
                    if (elemType != null)
                    {
                        typeParams = ExtractParameters(elemType);
                    }
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    element_id = elementId,
                    element_name = elem.Name,
                    category = elem.Category?.Name ?? "Uncategorized",
                    instance_parameters = instanceParams,
                    type_parameters = typeParams
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to retrieve element parameters: {ex.Message}"));
            }
        }

        private static List<object> ExtractParameters(Element element)
        {
            var list = new List<object>();
            foreach (Parameter p in element.Parameters)
            {
                if (p.Definition == null) continue;

                string valStr = p.AsValueString() ?? GetRawParameterValueString(p);
                list.Add(new
                {
                    name = p.Definition.Name,
                    storage_type = p.StorageType.ToString(),
                    is_read_only = p.IsReadOnly,
                    value = valStr
                });
            }
            return list;
        }

        private static string GetRawParameterValueString(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString() ?? string.Empty;
                case StorageType.Integer:
                    return p.AsInteger().ToString();
                case StorageType.Double:
                    return p.AsDouble().ToString(CultureInfo.InvariantCulture);
                case StorageType.ElementId:
                    return p.AsElementId().Value.ToString();
                default:
                    return "N/A";
            }
        }

        private static void HandleGetElementInfo(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            if (string.IsNullOrWhiteSpace(task.PayloadJson))
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("Payload JSON is empty."));
                return;
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(task.PayloadJson);
                if (!jsonDoc.RootElement.TryGetProperty("element_id", out var idProp))
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("Property 'element_id' is required."));
                    return;
                }

                long elementId = idProp.GetInt64();
                var doc = uiDoc.Document;
                var elem = doc.GetElement(new ElementId(elementId));

                if (elem == null)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson($"Element ID {elementId} not found."));
                    return;
                }

                string levelName = "N/A";
                if (elem.LevelId != ElementId.InvalidElementId)
                {
                    var level = doc.GetElement(elem.LevelId) as Level;
                    if (level != null) levelName = level.Name;
                }

                string typeName = "N/A";
                var typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    var elemType = doc.GetElement(typeId);
                    if (elemType != null) typeName = elemType.Name;
                }

                object? bboxObj = null;
                var bbox = elem.get_BoundingBox(null);
                if (bbox != null)
                {
                    bboxObj = new
                    {
                        min = new { x = bbox.Min.X, y = bbox.Min.Y, z = bbox.Min.Z },
                        max = new { x = bbox.Max.X, y = bbox.Max.Y, z = bbox.Max.Z }
                    };
                }

                string locationDesc = "N/A";
                if (elem.Location is LocationPoint locPt)
                {
                    locationDesc = $"Point({locPt.Point.X:F2}, {locPt.Point.Y:F2}, {locPt.Point.Z:F2})";
                }
                else if (elem.Location is LocationCurve locCurve)
                {
                    var p0 = locCurve.Curve.GetEndPoint(0);
                    var p1 = locCurve.Curve.GetEndPoint(1);
                    locationDesc = $"Curve(({p0.X:F2},{p0.Y:F2},{p0.Z:F2}) -> ({p1.X:F2},{p1.Y:F2},{p1.Z:F2}))";
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    element_id = elementId,
                    name = elem.Name,
                    category = elem.Category?.Name ?? "Uncategorized",
                    type_name = typeName,
                    level = levelName,
                    location = locationDesc,
                    bounding_box = bboxObj
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to retrieve element info: {ex.Message}"));
            }
        }

        private static void HandleGetElementsByCategory(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            if (string.IsNullOrWhiteSpace(task.PayloadJson))
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("Payload JSON is empty."));
                return;
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(task.PayloadJson);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("category_name", out var catProp))
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("Property 'category_name' is required."));
                    return;
                }

                string categoryName = catProp.GetString() ?? string.Empty;
                int limit = 100;
                if (root.TryGetProperty("limit", out var limitProp))
                {
                    limit = limitProp.GetInt32();
                }

                var doc = uiDoc.Document;
                var collector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType();

                var results = new List<object>();

                foreach (var elem in collector)
                {
                    if (elem.Category != null && elem.Category.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        string levelName = "N/A";
                        if (elem.LevelId != ElementId.InvalidElementId)
                        {
                            var level = doc.GetElement(elem.LevelId);
                            if (level != null) levelName = level.Name;
                        }

                        results.Add(new
                        {
                            element_id = elem.Id.Value,
                            name = elem.Name,
                            category = elem.Category.Name,
                            level = levelName
                        });

                        if (results.Count >= limit) break;
                    }
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    category_name = categoryName,
                    count = results.Count,
                    elements = results
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to query elements by category: {ex.Message}"));
            }
        }

        private static void HandleSetParameter(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            if (string.IsNullOrWhiteSpace(task.PayloadJson))
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("Payload JSON is empty."));
                return;
            }

            long elementId;
            string paramName;
            string valueStr;

            try
            {
                using var jsonDoc = JsonDocument.Parse(task.PayloadJson);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("element_id", out var elemIdProp) ||
                    !root.TryGetProperty("param_name", out var paramNameProp) ||
                    !root.TryGetProperty("value", out var valueProp))
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("Payload incomplete. Requires 'element_id', 'param_name', and 'value'."));
                    return;
                }

                elementId = elemIdProp.GetInt64();
                paramName = paramNameProp.GetString() ?? string.Empty;
                valueStr = valueProp.GetString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to parse payload JSON: {ex.Message}"));
                return;
            }

            var doc = uiDoc.Document;
            var targetElement = doc.GetElement(new ElementId(elementId));
            if (targetElement == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Element ID {elementId} not found."));
                return;
            }

            var parameter = targetElement.LookupParameter(paramName);
            if (parameter == null)
            {
                foreach (Parameter p in targetElement.Parameters)
                {
                    if (p.Definition != null && p.Definition.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))
                    {
                        parameter = p;
                        break;
                    }
                }
            }

            if (parameter == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Parameter '{paramName}' not found on element ID {elementId}."));
                return;
            }

            if (parameter.IsReadOnly)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Parameter '{paramName}' is Read-Only and cannot be modified."));
                return;
            }

            bool setSuccess = false;
            string failureReason = string.Empty;

            using (var tx = new Transaction(doc, "MCP Set Parameter"))
            {
                tx.Start();

                try
                {
                    switch (parameter.StorageType)
                    {
                        case StorageType.String:
                            setSuccess = parameter.Set(valueStr);
                            break;

                        case StorageType.Double:
                            if (double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal) ||
                                double.TryParse(valueStr, NumberStyles.Any, CultureInfo.CurrentCulture, out dVal))
                            {
                                setSuccess = parameter.Set(dVal);
                            }
                            else
                            {
                                failureReason = $"Value '{valueStr}' could not be converted to Double.";
                            }
                            break;

                        case StorageType.Integer:
                            if (int.TryParse(valueStr, out int iVal))
                            {
                                setSuccess = parameter.Set(iVal);
                            }
                            else if (bool.TryParse(valueStr, out bool bVal))
                            {
                                setSuccess = parameter.Set(bVal ? 1 : 0);
                            }
                            else
                            {
                                failureReason = $"Value '{valueStr}' could not be converted to Integer.";
                            }
                            break;

                        case StorageType.ElementId:
                            if (long.TryParse(valueStr, out long elIdVal))
                            {
                                setSuccess = parameter.Set(new ElementId(elIdVal));
                            }
                            else
                            {
                                failureReason = $"Value '{valueStr}' could not be converted to ElementId.";
                            }
                            break;

                        default:
                            failureReason = $"Parameter storage type '{parameter.StorageType}' is not supported.";
                            break;
                    }

                    if (setSuccess)
                    {
                        tx.Commit();
                    }
                    else
                    {
                        tx.RollBack();
                    }
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    failureReason = ex.Message;
                }
            }

            if (setSuccess)
            {
                var successJson = JsonSerializer.Serialize(new
                {
                    status = "success",
                    message = $"Parameter '{paramName}' updated successfully.",
                    element_id = elementId,
                    param_name = paramName,
                    new_value = valueStr
                });
                task.TaskCompletionSource.SetResult(successJson);
            }
            else
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to modify parameter '{paramName}': {failureReason}"));
            }
        }

        private static void HandleGetProjectInfo(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            var doc = uiDoc.Document;
            var projInfo = doc.ProjectInformation;

            var infoObj = new
            {
                status = "success",
                title = doc.Title,
                path = doc.PathName,
                project_name = projInfo?.Name ?? "N/A",
                project_number = projInfo?.Number ?? "N/A",
                client_name = projInfo?.ClientName ?? "N/A",
                building_name = projInfo?.BuildingName ?? "N/A",
                address = projInfo?.Address ?? "N/A",
                organization_name = projInfo?.OrganizationName ?? "N/A",
                organization_description = projInfo?.OrganizationDescription ?? "N/A"
            };

            task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(infoObj));
        }

        private static void HandleGetLevels(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            var doc = uiDoc.Document;
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .Select(l => new
                {
                    level_id = l.Id.Value,
                    name = l.Name,
                    elevation = l.Elevation,
                    elevation_formatted = $"{l.Elevation:F3} ft"
                })
                .ToList();

            task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
            {
                status = "success",
                count = levels.Count,
                levels = levels
            }));
        }

        private static void HandleGetViews(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            var doc = uiDoc.Document;
            var activeViewId = uiDoc.ActiveView?.Id;

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .Select(v => new
                {
                    view_id = v.Id.Value,
                    name = v.Name,
                    view_type = v.ViewType.ToString(),
                    is_active = v.Id == activeViewId
                })
                .ToList();

            task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
            {
                status = "success",
                count = views.Count,
                views = views
            }));
        }

        private static void HandleDeleteElements(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            if (string.IsNullOrWhiteSpace(task.PayloadJson))
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("Payload JSON is empty."));
                return;
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(task.PayloadJson);
                if (!jsonDoc.RootElement.TryGetProperty("element_ids", out var idsProp) || idsProp.ValueKind != JsonValueKind.Array)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("Property 'element_ids' must be an array of integers."));
                    return;
                }

                var idsList = new List<ElementId>();
                foreach (var item in idsProp.EnumerateArray())
                {
                    if (item.TryGetInt64(out long idVal))
                    {
                        idsList.Add(new ElementId(idVal));
                    }
                }

                if (idsList.Count == 0)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("'element_ids' list is empty."));
                    return;
                }

                var doc = uiDoc.Document;
                int deletedCount = 0;

                using (var tx = new Transaction(doc, "MCP Delete Elements"))
                {
                    tx.Start();
                    var deletedIds = doc.Delete(idsList);
                    deletedCount = deletedIds.Count;
                    tx.Commit();
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    message = $"{deletedCount} element(s) deleted from Revit model.",
                    deleted_count = deletedCount
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to delete elements: {ex.Message}"));
            }
        }

        private static string CreateErrorJson(string message)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                message
            });
        }

        public string GetName() => "RevitBridgeExternalHandler";
    }
}
