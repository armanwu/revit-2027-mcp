using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
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
        DeleteElements,
        GetAvailableTypes,
        CreateLevel,
        CreateGrid,
        CreateWall,
        CreateFloor,
        PlaceFamilyInstance,
        CreateRoom
    }

    public class BridgeTask
    {
        public TaskType Type { get; set; }
        public string? PayloadJson { get; set; }
        public TaskCompletionSource<string> TaskCompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public class RevitExternalHandler : IExternalEventHandler
    {
        private const double MmToFeet = 1.0 / 304.8;
        private const double FeetToMm = 304.8;

        private static double ToFeet(double mm) => mm * MmToFeet;
        private static double ToMm(double feet) => Math.Round(feet * FeetToMm, 2);

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

                        case TaskType.GetAvailableTypes:
                            HandleGetAvailableTypes(app, task);
                            break;

                        case TaskType.CreateLevel:
                            HandleCreateLevel(app, task);
                            break;

                        case TaskType.CreateGrid:
                            HandleCreateGrid(app, task);
                            break;

                        case TaskType.CreateWall:
                            HandleCreateWall(app, task);
                            break;

                        case TaskType.CreateFloor:
                            HandleCreateFloor(app, task);
                            break;

                        case TaskType.PlaceFamilyInstance:
                            HandlePlaceFamilyInstance(app, task);
                            break;

                        case TaskType.CreateRoom:
                            HandleCreateRoom(app, task);
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
                object? bboxMmObj = null;
                var bbox = elem.get_BoundingBox(null);
                if (bbox != null)
                {
                    bboxObj = new
                    {
                        min = new { x = bbox.Min.X, y = bbox.Min.Y, z = bbox.Min.Z },
                        max = new { x = bbox.Max.X, y = bbox.Max.Y, z = bbox.Max.Z }
                    };
                    bboxMmObj = new
                    {
                        min_mm = new { x = ToMm(bbox.Min.X), y = ToMm(bbox.Min.Y), z = ToMm(bbox.Min.Z) },
                        max_mm = new { x = ToMm(bbox.Max.X), y = ToMm(bbox.Max.Y), z = ToMm(bbox.Max.Z) }
                    };
                }

                string locationDesc = "N/A";
                object? locationMmObj = null;
                if (elem.Location is LocationPoint locPt)
                {
                    locationDesc = $"Point({locPt.Point.X:F2}, {locPt.Point.Y:F2}, {locPt.Point.Z:F2})";
                    locationMmObj = new
                    {
                        type = "Point",
                        x_mm = ToMm(locPt.Point.X),
                        y_mm = ToMm(locPt.Point.Y),
                        z_mm = ToMm(locPt.Point.Z)
                    };
                }
                else if (elem.Location is LocationCurve locCurve)
                {
                    var p0 = locCurve.Curve.GetEndPoint(0);
                    var p1 = locCurve.Curve.GetEndPoint(1);
                    locationDesc = $"Curve(({p0.X:F2},{p0.Y:F2},{p0.Z:F2}) -> ({p1.X:F2},{p1.Y:F2},{p1.Z:F2}))";
                    locationMmObj = new
                    {
                        type = "Curve",
                        start_mm = new { x = ToMm(p0.X), y = ToMm(p0.Y), z = ToMm(p0.Z) },
                        end_mm = new { x = ToMm(p1.X), y = ToMm(p1.Y), z = ToMm(p1.Z) },
                        length_mm = ToMm(locCurve.Curve.Length)
                    };
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
                    location_mm = locationMmObj,
                    bounding_box = bboxObj,
                    bounding_box_mm = bboxMmObj
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
                    elevation_mm = ToMm(l.Elevation),
                    elevation_formatted = $"{ToMm(l.Elevation):F0} mm ({l.Elevation:F3} ft)"
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

        private static Level? ResolveLevel(Document doc, JsonElement root)
        {
            var allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (allLevels.Count == 0) return null;

            if (root.TryGetProperty("level_id", out var idProp) && idProp.ValueKind == JsonValueKind.Number && idProp.TryGetInt64(out long lvlId))
            {
                var byId = allLevels.FirstOrDefault(l => l.Id.Value == lvlId);
                if (byId != null) return byId;
            }

            if (root.TryGetProperty("level_name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                string lvlName = nameProp.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(lvlName))
                {
                    var exact = allLevels.FirstOrDefault(l => l.Name.Equals(lvlName, StringComparison.OrdinalIgnoreCase));
                    if (exact != null) return exact;

                    if (long.TryParse(lvlName, out long parsedId))
                    {
                        var byParsedId = allLevels.FirstOrDefault(l => l.Id.Value == parsedId);
                        if (byParsedId != null) return byParsedId;
                    }

                    var partial = allLevels.FirstOrDefault(l => l.Name.Contains(lvlName, StringComparison.OrdinalIgnoreCase));
                    if (partial != null) return partial;
                }
            }

            return allLevels.First();
        }

        private static void HandleGetAvailableTypes(UIApplication app, BridgeTask task)
        {
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson("No active Revit document."));
                return;
            }

            try
            {
                string categoryFilter = string.Empty;
                int limit = 200;

                if (!string.IsNullOrWhiteSpace(task.PayloadJson))
                {
                    using var jsonDoc = JsonDocument.Parse(task.PayloadJson);
                    var root = jsonDoc.RootElement;
                    if (root.TryGetProperty("category_name", out var catProp) && catProp.ValueKind == JsonValueKind.String)
                    {
                        categoryFilter = catProp.GetString() ?? string.Empty;
                    }
                    if (root.TryGetProperty("limit", out var limitProp) && limitProp.ValueKind == JsonValueKind.Number)
                    {
                        limit = limitProp.GetInt32();
                    }
                }

                var doc = uiDoc.Document;
                var collector = new FilteredElementCollector(doc)
                    .WhereElementIsElementType();

                var typesList = new List<object>();

                foreach (var elem in collector)
                {
                    if (elem is not ElementType elemType || elemType.Category == null) continue;

                    string catName = elemType.Category.Name;
                    if (!string.IsNullOrWhiteSpace(categoryFilter) &&
                        !catName.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase) &&
                        !catName.Contains(categoryFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    typesList.Add(new
                    {
                        type_id = elemType.Id.Value,
                        family_name = elemType.FamilyName ?? "System Family",
                        type_name = elemType.Name,
                        full_name = $"{elemType.FamilyName}: {elemType.Name}",
                        category = catName
                    });

                    if (typesList.Count >= limit) break;
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    category_filter = string.IsNullOrWhiteSpace(categoryFilter) ? "ALL" : categoryFilter,
                    count = typesList.Count,
                    types = typesList
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to retrieve available element types: {ex.Message}"));
            }
        }

        private static void HandleCreateLevel(UIApplication app, BridgeTask task)
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

                if (!root.TryGetProperty("elevation_mm", out var elevProp) || !elevProp.TryGetDouble(out double elevationMm))
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("Property 'elevation_mm' (number) is required."));
                    return;
                }

                string? levelName = null;
                if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    levelName = nameProp.GetString();
                }

                var doc = uiDoc.Document;
                Level newLevel;

                using (var tx = new Transaction(doc, "MCP Create Level"))
                {
                    tx.Start();
                    newLevel = Level.Create(doc, ToFeet(elevationMm));
                    if (!string.IsNullOrWhiteSpace(levelName))
                    {
                        newLevel.Name = levelName;
                    }
                    tx.Commit();
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    message = $"Level '{newLevel.Name}' created at {elevationMm} mm.",
                    level_id = newLevel.Id.Value,
                    name = newLevel.Name,
                    elevation_mm = ToMm(newLevel.Elevation),
                    elevation_ft = newLevel.Elevation
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to create level: {ex.Message}"));
            }
        }

        private static void HandleCreateGrid(UIApplication app, BridgeTask task)
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

                double startX = root.GetProperty("start_x_mm").GetDouble();
                double startY = root.GetProperty("start_y_mm").GetDouble();
                double endX = root.GetProperty("end_x_mm").GetDouble();
                double endY = root.GetProperty("end_y_mm").GetDouble();

                string? gridName = null;
                if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    gridName = nameProp.GetString();
                }

                var p0 = new XYZ(ToFeet(startX), ToFeet(startY), 0);
                var p1 = new XYZ(ToFeet(endX), ToFeet(endY), 0);
                var line = Line.CreateBound(p0, p1);

                var doc = uiDoc.Document;
                Grid newGrid;

                using (var tx = new Transaction(doc, "MCP Create Grid"))
                {
                    tx.Start();
                    newGrid = Grid.Create(doc, line);
                    if (!string.IsNullOrWhiteSpace(gridName))
                    {
                        newGrid.Name = gridName;
                    }
                    tx.Commit();
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    message = $"Grid '{newGrid.Name}' created.",
                    grid_id = newGrid.Id.Value,
                    name = newGrid.Name,
                    start_mm = new { x = startX, y = startY },
                    end_mm = new { x = endX, y = endY }
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to create grid: {ex.Message}"));
            }
        }

        private static void HandleCreateWall(UIApplication app, BridgeTask task)
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

                double startX = root.GetProperty("start_x_mm").GetDouble();
                double startY = root.GetProperty("start_y_mm").GetDouble();
                double endX = root.GetProperty("end_x_mm").GetDouble();
                double endY = root.GetProperty("end_y_mm").GetDouble();

                double heightMm = 3000.0;
                if (root.TryGetProperty("height_mm", out var hProp) && hProp.ValueKind == JsonValueKind.Number)
                {
                    heightMm = hProp.GetDouble();
                }

                double offsetMm = 0.0;
                if (root.TryGetProperty("offset_mm", out var offProp) && offProp.ValueKind == JsonValueKind.Number)
                {
                    offsetMm = offProp.GetDouble();
                }

                bool structural = false;
                if (root.TryGetProperty("structural", out var structProp) &&
                    (structProp.ValueKind == JsonValueKind.True || structProp.ValueKind == JsonValueKind.False))
                {
                    structural = structProp.GetBoolean();
                }

                var doc = uiDoc.Document;
                var level = ResolveLevel(doc, root);
                if (level == null)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("No Levels found in document to host the wall."));
                    return;
                }

                WallType? selectedWallType = null;
                if (root.TryGetProperty("wall_type_name", out var wtProp) && wtProp.ValueKind == JsonValueKind.String)
                {
                    string wtName = wtProp.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(wtName))
                    {
                        var wallTypes = new FilteredElementCollector(doc)
                            .OfClass(typeof(WallType))
                            .Cast<WallType>()
                            .ToList();

                        selectedWallType = wallTypes.FirstOrDefault(w =>
                            w.Name.Equals(wtName, StringComparison.OrdinalIgnoreCase) ||
                            $"{w.FamilyName}: {w.Name}".Equals(wtName, StringComparison.OrdinalIgnoreCase))
                            ?? wallTypes.FirstOrDefault(w => w.Name.Contains(wtName, StringComparison.OrdinalIgnoreCase));
                    }
                }

                var p0 = new XYZ(ToFeet(startX), ToFeet(startY), level.Elevation);
                var p1 = new XYZ(ToFeet(endX), ToFeet(endY), level.Elevation);
                var wallLine = Line.CreateBound(p0, p1);

                Wall newWall;
                using (var tx = new Transaction(doc, "MCP Create Wall"))
                {
                    tx.Start();
                    ElementId wallTypeId = selectedWallType?.Id ?? doc.GetDefaultElementTypeId(ElementTypeGroup.WallType);
                    newWall = Wall.Create(doc, wallLine, wallTypeId, level.Id, ToFeet(heightMm), ToFeet(offsetMm), false, structural);
                    tx.Commit();
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    message = $"Wall created (ID: {newWall.Id.Value}) on level '{level.Name}'.",
                    element_id = newWall.Id.Value,
                    name = newWall.Name,
                    wall_type = newWall.WallType?.Name ?? "Default",
                    level = level.Name,
                    height_mm = heightMm,
                    length_mm = ToMm(wallLine.Length),
                    start_mm = new { x = startX, y = startY },
                    end_mm = new { x = endX, y = endY }
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to create wall: {ex.Message}"));
            }
        }

        private static void HandleCreateFloor(UIApplication app, BridgeTask task)
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

                if (!root.TryGetProperty("points_mm", out var ptsProp) || ptsProp.ValueKind != JsonValueKind.Array)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("Property 'points_mm' must be an array of [x_mm, y_mm] or {\"x\": x_mm, \"y\": y_mm} coordinates."));
                    return;
                }

                var doc = uiDoc.Document;
                var level = ResolveLevel(doc, root);
                if (level == null)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("No Levels found in document to host the floor."));
                    return;
                }

                var xyzPoints = new List<XYZ>();
                foreach (var ptElem in ptsProp.EnumerateArray())
                {
                    double xMm, yMm;
                    if (ptElem.ValueKind == JsonValueKind.Array)
                    {
                        var arr = ptElem.EnumerateArray().ToList();
                        if (arr.Count < 2) continue;
                        xMm = arr[0].GetDouble();
                        yMm = arr[1].GetDouble();
                    }
                    else if (ptElem.ValueKind == JsonValueKind.Object)
                    {
                        xMm = ptElem.GetProperty("x").GetDouble();
                        yMm = ptElem.GetProperty("y").GetDouble();
                    }
                    else
                    {
                        continue;
                    }

                    xyzPoints.Add(new XYZ(ToFeet(xMm), ToFeet(yMm), level.Elevation));
                }

                if (xyzPoints.Count < 3)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("At least 3 boundary points are required to create a floor."));
                    return;
                }

                // Remove duplicate closing point if caller passed start point again at the end
                if (xyzPoints.Count > 3 && xyzPoints.First().IsAlmostEqualTo(xyzPoints.Last()))
                {
                    xyzPoints.RemoveAt(xyzPoints.Count - 1);
                }

                var curveLoop = new CurveLoop();
                for (int i = 0; i < xyzPoints.Count; i++)
                {
                    var pStart = xyzPoints[i];
                    var pEnd = xyzPoints[(i + 1) % xyzPoints.Count];
                    curveLoop.Append(Line.CreateBound(pStart, pEnd));
                }

                ElementId floorTypeId = ElementId.InvalidElementId;
                if (root.TryGetProperty("floor_type_name", out var ftProp) && ftProp.ValueKind == JsonValueKind.String)
                {
                    string ftName = ftProp.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(ftName))
                    {
                        var floorTypes = new FilteredElementCollector(doc)
                            .OfClass(typeof(FloorType))
                            .Cast<FloorType>()
                            .Where(ft => !ft.IsFoundationSlab)
                            .ToList();

                        var matched = floorTypes.FirstOrDefault(f =>
                            f.Name.Equals(ftName, StringComparison.OrdinalIgnoreCase) ||
                            $"{f.FamilyName}: {f.Name}".Equals(ftName, StringComparison.OrdinalIgnoreCase))
                            ?? floorTypes.FirstOrDefault(f => f.Name.Contains(ftName, StringComparison.OrdinalIgnoreCase));

                        if (matched != null) floorTypeId = matched.Id;
                    }
                }

                if (floorTypeId == ElementId.InvalidElementId)
                {
                    floorTypeId = Floor.GetDefaultFloorType(doc, false);
                }

                Floor newFloor;
                using (var tx = new Transaction(doc, "MCP Create Floor"))
                {
                    tx.Start();
                    newFloor = Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorTypeId, level.Id);
                    tx.Commit();
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    message = $"Floor created (ID: {newFloor.Id.Value}) on level '{level.Name}'.",
                    element_id = newFloor.Id.Value,
                    name = newFloor.Name,
                    floor_type = newFloor.FloorType?.Name ?? "Default",
                    level = level.Name,
                    vertex_count = xyzPoints.Count
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to create floor: {ex.Message}"));
            }
        }

        private static void HandlePlaceFamilyInstance(UIApplication app, BridgeTask task)
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

                double xMm = root.GetProperty("x_mm").GetDouble();
                double yMm = root.GetProperty("y_mm").GetDouble();
                double zMm = 0.0;
                if (root.TryGetProperty("z_mm", out var zProp) && zProp.ValueKind == JsonValueKind.Number)
                {
                    zMm = zProp.GetDouble();
                }

                double rotationDeg = 0.0;
                if (root.TryGetProperty("rotation_deg", out var rotProp) && rotProp.ValueKind == JsonValueKind.Number)
                {
                    rotationDeg = rotProp.GetDouble();
                }

                string typeNameFilter = root.TryGetProperty("type_name", out var tnProp) && tnProp.ValueKind == JsonValueKind.String
                    ? (tnProp.GetString() ?? string.Empty)
                    : string.Empty;

                string familyOrCategoryFilter = root.TryGetProperty("category_or_family_name", out var cfProp) && cfProp.ValueKind == JsonValueKind.String
                    ? (cfProp.GetString() ?? string.Empty)
                    : string.Empty;

                var doc = uiDoc.Document;
                var level = ResolveLevel(doc, root);
                if (level == null)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("No Levels found in document."));
                    return;
                }

                var symbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .ToList();

                FamilySymbol? matchedSymbol = null;

                if (root.TryGetProperty("type_id", out var tidProp) && tidProp.ValueKind == JsonValueKind.Number && tidProp.TryGetInt64(out long typeIdVal))
                {
                    matchedSymbol = symbols.FirstOrDefault(s => s.Id.Value == typeIdVal);
                }

                if (matchedSymbol == null)
                {
                    var candidates = symbols.AsEnumerable();

                    if (!string.IsNullOrWhiteSpace(familyOrCategoryFilter))
                    {
                        var filtered = candidates.Where(s =>
                            (s.FamilyName != null && s.FamilyName.Contains(familyOrCategoryFilter, StringComparison.OrdinalIgnoreCase)) ||
                            (s.Category != null && s.Category.Name.Contains(familyOrCategoryFilter, StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        if (filtered.Count > 0) candidates = filtered;
                    }

                    if (!string.IsNullOrWhiteSpace(typeNameFilter))
                    {
                        matchedSymbol = candidates.FirstOrDefault(s =>
                            s.Name.Equals(typeNameFilter, StringComparison.OrdinalIgnoreCase) ||
                            $"{s.FamilyName}: {s.Name}".Equals(typeNameFilter, StringComparison.OrdinalIgnoreCase))
                            ?? candidates.FirstOrDefault(s => s.Name.Contains(typeNameFilter, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        matchedSymbol = candidates.FirstOrDefault();
                    }
                }

                if (matchedSymbol == null)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson(
                        $"No matching FamilySymbol found for category/family '{familyOrCategoryFilter}' and type '{typeNameFilter}'. Use get_available_types() to inspect loaded families."));
                    return;
                }

                Element? hostElement = null;
                if (root.TryGetProperty("host_element_id", out var hostProp) && hostProp.ValueKind == JsonValueKind.Number && hostProp.TryGetInt64(out long hostIdVal) && hostIdVal > 0)
                {
                    hostElement = doc.GetElement(new ElementId(hostIdVal));
                }

                FamilyInstance instance;
                using (var tx = new Transaction(doc, "MCP Place Family Instance"))
                {
                    tx.Start();

                    if (!matchedSymbol.IsActive)
                    {
                        matchedSymbol.Activate();
                        doc.Regenerate();
                    }

                    var locationPt = new XYZ(ToFeet(xMm), ToFeet(yMm), level.Elevation + ToFeet(zMm));

                    if (hostElement != null)
                    {
                        if (hostElement.Location is LocationCurve hostLocCurve)
                        {
                            var proj = hostLocCurve.Curve.Project(new XYZ(locationPt.X, locationPt.Y, hostLocCurve.Curve.GetEndPoint(0).Z));
                            if (proj != null)
                            {
                                locationPt = new XYZ(proj.XYZPoint.X, proj.XYZPoint.Y, level.Elevation + ToFeet(zMm));
                            }
                        }

                        instance = doc.Create.NewFamilyInstance(locationPt, matchedSymbol, hostElement, level, StructuralType.NonStructural);
                    }
                    else
                    {
                        var stType = matchedSymbol.Category != null &&
                                     matchedSymbol.Category.Name.Contains("Structural", StringComparison.OrdinalIgnoreCase)
                            ? StructuralType.Column
                            : StructuralType.NonStructural;

                        instance = doc.Create.NewFamilyInstance(locationPt, matchedSymbol, level, stType);
                    }

                    if (Math.Abs(rotationDeg) > 0.001 && hostElement == null)
                    {
                        var axis = Line.CreateBound(locationPt, locationPt + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, instance.Id, axis, rotationDeg * Math.PI / 180.0);
                    }

                    tx.Commit();
                }

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    message = $"Placed '{matchedSymbol.FamilyName}: {matchedSymbol.Name}' (ID: {instance.Id.Value}).",
                    element_id = instance.Id.Value,
                    family_name = matchedSymbol.FamilyName,
                    type_name = matchedSymbol.Name,
                    category = instance.Category?.Name ?? "Uncategorized",
                    level = level.Name,
                    host_element_id = hostElement?.Id.Value
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to place family instance: {ex.Message}"));
            }
        }

        private static void HandleCreateRoom(UIApplication app, BridgeTask task)
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

                double xMm = root.GetProperty("x_mm").GetDouble();
                double yMm = root.GetProperty("y_mm").GetDouble();

                string? roomName = root.TryGetProperty("room_name", out var rnProp) && rnProp.ValueKind == JsonValueKind.String
                    ? rnProp.GetString()
                    : null;

                string? roomNumber = root.TryGetProperty("room_number", out var rnumProp) && rnumProp.ValueKind == JsonValueKind.String
                    ? rnumProp.GetString()
                    : null;

                var doc = uiDoc.Document;
                var level = ResolveLevel(doc, root);
                if (level == null)
                {
                    task.TaskCompletionSource.SetResult(CreateErrorJson("No Levels found in document to host the room."));
                    return;
                }

                Room newRoom;
                using (var tx = new Transaction(doc, "MCP Create Room"))
                {
                    tx.Start();
                    var uvPt = new UV(ToFeet(xMm), ToFeet(yMm));
                    newRoom = doc.Create.NewRoom(level, uvPt);

                    if (!string.IsNullOrWhiteSpace(roomName))
                    {
                        newRoom.Name = roomName;
                    }
                    if (!string.IsNullOrWhiteSpace(roomNumber))
                    {
                        newRoom.Number = roomNumber;
                    }
                    tx.Commit();
                }

                // 1 sq ft = 0.09290304 sq m
                double areaSqm = Math.Round(newRoom.Area * 0.09290304, 2);

                task.TaskCompletionSource.SetResult(JsonSerializer.Serialize(new
                {
                    status = "success",
                    message = $"Room '{newRoom.Name}' ({newRoom.Number}) created on level '{level.Name}'.",
                    element_id = newRoom.Id.Value,
                    room_name = newRoom.Name,
                    room_number = newRoom.Number,
                    level = level.Name,
                    area_sqm = areaSqm
                }));
            }
            catch (Exception ex)
            {
                task.TaskCompletionSource.SetResult(CreateErrorJson($"Failed to create room: {ex.Message}"));
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
