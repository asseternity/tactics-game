@tool
extends RefCounted
class_name TranslationManager

# 单例实例
static var instance = TranslationManager.new()

# 当前语言
static var current_language = "zh"

# 语言变化监听器列表
static var language_change_listeners: Array[Callable] = []

# 翻译字典
static var translations = {
	"zh": {
		# 通用UI
		"tree_view": "树形视图",
		"table_view": "表格视图",
		"tree_view_with_edit": "树形视图 (双击编辑):",
		"excel_style_table_view": "Excel风格表格视图:",
		"add_row": "添加行",
		"key": "键",
		"value": "值",
		"value_header": "值",
		"index": "索引",
		
		# 右键菜单
		"right_click_row_menu": "右键显示行操作菜单",
		"add_new_row_before": "添加新行 (在此行之前)",
		"add_new_row_after": "添加新行 (在此行之后)",
		"copy_this_row": "复制此行",
		"delete_this_row": "删除此行",
		"enter_value": "输入值...",

		# 单元格展开功能
		"double_click_to_expand": "双击展开查看完整内容",
		"cell_content_viewer": "单元格内容查看器 - 行%s列%s",
		"cell_position_info": "位置: 第%s行, 第%s列",
		"enter_cell_content": "输入单元格内容...",
		"confirm": "确认",

		# 浮动窗口展开功能
		"content_expansion": "内容展开",
		"content_expansion_level": "内容展开 (级别 %d)",
		"table_mode": "📊 表格",
		"text_mode": "📝 文本",
		"content_placeholder": "内容将在这里显示...",
		"double_click_expand_tooltip": "🔍 双击展开查看完整内容",
		"level_expansion": "%s 级别 %d 展开",

		# 内联展开功能
		"collapse": "收起",
		"edit_mode": "编辑模式",
		"view_mode": "查看模式",

		# 实时输入同步功能
		"realtime_input_sync_enabled": "实时输入同步: 启用",
		"realtime_input_sync_disabled": "实时输入同步: 禁用",
		"realtime_input_detected": "实时输入检测",
		"realtime_update_data": "实时更新数据",
		"floating_window_realtime_input": "浮动窗口实时输入",

		# 性能优化功能
		"delayed_sync_timer_initialized": "延迟同步定时器已初始化",
		"delayed_sync_requested": "请求延迟同步",
		"delayed_sync_executing": "执行延迟同步...",
		"delayed_sync_completed": "延迟同步完成",
		"immediate_sync_executing": "执行立即同步...",
		"immediate_sync_completed": "立即同步完成",
		"sync_delay_set": "同步延迟时间已设置为",
		"realtime_sync_delay_set": "实时同步延迟时间已设置为",

		# 内容展开检测
		"content_too_short": "内容太短，不需要展开",
		"content_suitable_for_expansion": "内容适合展开，在下方展开显示",
		"content_suitable_for_floating_expansion": "内容适合展开，级别",
		"max_expansion_level_reached": "已达到最大展开层级，无法继续展开",
		"child_expander_created": "创建子展开器",
		"child_expander_closed": "子展开器已关闭",
		"source_info_recorded": "记录子窗口来源信息",
		"source_info_found": "找到子窗口来源信息",
		"source_info_not_found": "警告：未找到子窗口的来源信息",
		"cleanup_source_mapping": "清理子窗口来源映射",

		# 表格标题和标签
		"table_header_key": "键",
		"table_header_value": "值",
		"table_header_index": "索引",
		"seconds": "秒",

		# 弹出窗口按钮和标签
		"cancel_button": "❌ 取消",
		"confirm_button": "✅ 确认",
		"no_table_data": "无法解析为表格数据",
		"array_empty": "数组为空",
		"input_value_placeholder": "输入值...",

		# Excel表格中的硬编码文本
		"floating_window_mode_enabled": "浮动窗口模式已启用",
		"expansion_content_update_start": "=== 开始处理展开器内容更新 ===",
		"new_content": "新内容",
		"source_cell_validity": "source_cell 有效性",
		"cell_position": "单元格位置: 行",
		"column": "列",
		"direct_call_update_cell": "直接调用 _update_cell_value",
		"cell_content_updated": "单元格内容已更新并发送信号",
		"error_source_cell_invalid": "错误：source_cell 无效",
		"error_no_source_cell": "错误：current_expanded_cell 中没有 source_cell",
		"expansion_content_update_complete": "=== 展开器内容更新处理完成 ===",
		"expander_closed": "展开器已关闭",
		"wait_next_frame": "等待下一帧确保节点被清理",
		"infer_column_type": "推断每列的类型",
		"infer_key_value_type": "为键值对模式推断类型",
		"infer_array_column_type": "为数组中的对象推断列类型",
		"infer_simple_array_type": "为简单数组推断类型",
		"add_row_number_column": "添加行号列",
		"add_row_number": "添加行号",
		"deeper_blue": "更深的蓝色",
		"add_gradient_effect": "添加渐变效果",
		"add_type_icon": "添加类型图标到列标题",
		"edit_icon": " ✎",
		"data_column_double_click": "如果是数据列（非行号列），添加双击编辑功能",
		"add_mouse_detection": "为标题添加鼠标检测",
		"gray_color": "灰色，区别于普通标题",
		"hover_style": "悬停样式",
		"add_right_click_menu": "添加右键菜单功能",
		"normal_state_style": "普通状态样式",
		"focus_state_style": "焦点状态样式",
		"add_shadow_effect": "添加阴影效果",
		"hover_state_style": "悬停状态样式",
		"font_style": "字体样式",
		"add_expansion_tooltip": "为单元格添加展开提示到tooltip",
		"realtime_text_change": "处理单元格文本实时变化",
		"detected_double_click": "检测到双击事件，行",
		"cell_content": "单元格内容",
		"content_length": "内容长度",
		"update_cell_value_called": "=== _update_cell_value 被调用 ===",
		"parameters": "参数",
		"rows_data_size": "rows_data.size()=",
		"before_update": "更新前",
		"after_update": "更新后",
		"update_ui_cell": "同时更新UI单元格显示",
		"error_column_out_of_range": "错误：列索引超出范围 col=",
		"error_row_out_of_range": "错误：行索引超出范围 row=",
		"update_cell_value_complete": "=== _update_cell_value 处理完成 ===",
		"emit_data_change_signal": "发出数据变化信号，这会触发主编辑器的保存逻辑",
		"parse_value_by_type": "根据指定的类型解析值",
		"cannot_convert_return_zero": "无法转换时返回0",
		"non_empty_string_true": "非空字符串为true",
		"direct_emit_column_edit": "直接发出列编辑请求信号，让主编辑器处理",
		"convert_column_type": "转换指定列的数据类型",
		"update_column_type_record": "更新列类型记录",
		"convert_single_value_type": "转换单个值的类型",
		"force_convert_extract_number": "强制转换：尝试提取数字，失败则为0",
		"smart_convert_keep_original": "智能转换：无法转换则保持原值",
		"force_convert_non_empty_true": "强制转换：非空字符串为true",
		"smart_convert_unrecognized_keep": "智能转换：无法识别则保持原值",
		"convert_current_data_column": "直接转换current_data中指定列的数据类型",
		"convert_column": "转换列",
		"to_type": "到类型",
		"convert_dictionary_column": "转换字典数据中的指定列",
		"convert": "转换",
		"convert_value": "转换值",
		"convert_array_column": "转换数组数据中的指定列",
		"convert_object": "转换对象",
		"default_string": "默认为字符串",
		"boolean_type": "布尔类型",
		"number_type": "数字类型",
		"default_string_type": "默认为字符串类型",
		"get_column_type_icon": "获取列类型的图标",
		"max_samples": "最多显示5个示例",
		"key_value_mode": "键值对模式",
		"simple_array_mode": "简单数组模式",
		"object_array_mode": "对象数组模式",
		"smart_conversion_arrow": "智能转换 → ",
		"force_conversion_arrow": "强制转换 → ",
		"floating_window_text_realtime_input": "浮动窗口文本实时输入",

		# 单元格展开功能中的新增硬编码文本
		"current_expanded_cell": "current_expanded_cell",
		"update_before": "更新前",
		"update_after": "更新后",
		"cell_position_row": "单元格位置: 行",
		"column_text": "列",
		"signal_sent_backup": "同时也发送信号作为备用",
		"cell_content_updated_signal": "单元格内容已更新并发送信号",
		"detected_double_click_row": "检测到双击事件，行",
		"cell_content_text": "单元格内容",
		"content_length_text": "内容长度",
		"release_focus_prevent_edit": "暂时释放焦点，防止编辑模式干扰",
		"expand_cell_content_floating": "展开单元格内容到浮动窗口，行",
		"save_expanded_cell_info": "保存当前展开的单元格信息",
		"create_floating_expander": "创建浮动展开器窗口",
		"connect_signals": "连接信号",
		"setup_expansion_info": "设置展开信息",
		"set_window_title": "设置窗口标题",
		"cell_content_title": "📋 单元格内容 - 行%d列%d (%s)",
		"cell_expansion_title": "单元格展开 - 行%d列%d",
		"realtime_update_object_collection": "实时更新对象集合",
		"realtime_update_key_value": "实时更新键值对",
		"realtime_update_simple_array": "实时更新简单数组",
		"realtime_update_object_array": "实时更新对象数组",

		# 硬编码文本修复
		"key_value_pair_mode": "键值对模式",
		"value_column": "值列",
		"key_column": "键列",
		"check_all_values_boolean": "检查是否所有值都是布尔类型",
		"check_all_values_number": "检查是否所有值都是数字类型",
		"infer_simple_values_type": "推断简单值列表的类型",
		"key_value_pair_operations": "键值对模式的行操作",
		"insert_new_key_value_pair": "插入新键值对",
		"add_at_end": "在末尾添加新键值对",
		"copy_key_value_pair": "复制键值对",
		"delete_key_value_pair": "删除键值对",
		"add_new_value_to_array": "已在数组末尾添加新值",
		"insert_new_value_at_index": "已在索引插入新值",
		"copy_array_element": "已复制数组元素",
		"delete_array_element": "已删除数组元素",
		"object_collection_mode_update_keys": "对象集合模式：更新所有对象中的键名",
		"object_array_mode_update_keys": "对象数组模式：更新数组中所有对象的键名",
		"generate_unique_key_name": "生成唯一键名",
		"update_key_column": "更新键",
		"update_value_column": "更新值",
		"simple_array_value_column": "简单数组值列",
		"keyboard_navigation": "处理键盘导航",
		"shortcut_key_save": "检测到快捷键保存",
		"from_index": "从索引",
		"to": "到",
		"row_number_header": "行号",
		"get_godot_type_name": "获取Godot类型名称",
		"row_number_cell_events": "行号单元格事件处理",
		"show_row_context_menu": "显示行操作的右键菜单",
		"auto_cleanup": "自动清理",
		"handle_row_menu_selection": "处理行操作菜单选择",
		"menu_selection_id": "菜单选择 - ID: ",
		"row_number": ", 行号: ",
		"execute_add_before": "执行：在此行之前添加",
		"execute_add_after": "执行：在此行之后添加",
		"execute_copy_row": "执行：复制此行",
		"execute_delete_row": "执行：删除此行",
		"add_row_to_index": "添加行到索引: ",
		"copy_row_index": "复制行索引: ",
		"delete_row_index": "删除行索引: ",
		"auto_cleanup_connect": "自动清理",
		"object_collection_mode": "===== 对象集合模式的行操作 =====",
		"object_collection_add_row": "对象集合模式 - 添加行到索引: ",
		"insert_at_position": "在位置 ",
		"insert_new_object_id": " 插入新对象，ID: ",
		"add_to_end_id": "在末尾添加新对象，ID: ",
		"added_new_object_id": "已添加新对象，ID: ",
		"data": ", 数据: ",
		"object_collection_copy_row": "对象集合模式 - 复制行索引: ",
		"error_empty_row_data": "错误：要复制的行数据为空",
		"error_original_object_not_found": "错误：找不到原始对象，ID: ",
		"copied_object_original_id": "已复制对象，原ID: ",
		"new_id": ", 新ID: ",
		"object_collection_delete_row": "对象集合模式 - 删除行索引: ",
		"error_row_index_out_of_range": "错误：行索引超出范围: ",
		"deleted_object_id": "已删除对象，ID: ",
		"key_value_pairs_mode": "===== 键值对模式的行操作 =====",
		"key_value_add_row": "键值对模式 - 添加行到索引: ",
		"insert_new_key_value": " 插入新键值对，键: ",
		"add_to_end_key": "在末尾添加新键值对，键: ",
		"added_new_key_value": "已添加新键值对，键: ",
		"value_suffix": ", 值: ",
		"key_value_copy_row": "键值对模式 - 复制行索引: ",
		"error_incomplete_row_data": "错误：要复制的行数据不完整",
		"copied_key_value_original": "已复制键值对，原键: ",
		"new_key": ", 新键: ",
		"key_value_delete_row": "键值对模式 - 删除行索引: ",
		"deleted_key_value": "已删除键值对，键: ",
		"simple_array_mode_ops": "===== 简单数组模式的行操作 =====",
		"simple_array_add_row": "简单数组模式 - 添加行到索引: ",
		"added_to_array_end": "已在数组末尾添加新值: ",
		"inserted_at_index": "已在索引 ",
		"insert_new_value": " 插入新值: ",
		"simple_array_copy_row": "简单数组模式 - 复制行索引: ",
		"copied_array_element": "已复制数组元素，索引 ",
		"value_to_index": " 的值 ",
		"to_index": " 到索引 ",
		"simple_array_delete_row": "简单数组模式 - 删除行索引: ",
		"deleted_array_element": "已删除数组元素，索引 ",
		"value_colon": " 的值: ",
		"object_array_mode_ops": "===== 对象数组模式的行操作 =====",
		"object_array_add_row": "对象数组模式 - 添加行到索引: ",
		"added_to_array_end_object": "已在数组末尾添加新对象: ",
		"inserted_at_index_object": "已在索引 ",
		"insert_new_object": " 插入新对象: ",
		"object_array_copy_row": "对象数组模式 - 复制行索引: ",
		"copied_object_from_index": "已复制对象，从索引 ",
		"to_index_object": " 到索引 ",
		"object_colon": ", 对象: ",
		"error_not_object_type": "错误：要复制的元素不是对象类型",
		"object_array_delete_row": "对象数组模式 - 删除行索引: ",
		"deleted_object_index": "已删除对象，索引 ",
		"object_colon_deleted": " 的对象: ",
		"helper_functions": "===== 辅助函数 =====",
		"new_value": "新值",
		"column_rename_functions": "===== 列标题编辑辅助函数 =====",
		"rename_column": "重命名列",
		"rename_column_from_to": "重命名列 ",
		"update_data_column_names": "更新数据中的列名",
		"object_array_update_keys": "对象数组模式：更新数组中所有对象的键名",
		"simple_expansion_dialog": "简单的单元格展开对话框",
		"create_simple_expansion_dialog": "创建简单展开对话框",
		"add_to_dialog": "添加到对话框",
		"user_confirmed_update": "用户确认，更新单元格内容",
		"user_cancelled": "用户取消",
		"simple_expansion_dialog_shown": "简单展开对话框已显示",
		"confirm_button_style": "确认按钮样式",

		"after_execution": "后执行",
		"delayed_sync_mode": "延迟同步模式",
		
		# 状态消息
		"data_modified_save_reminder": "数据已修改，请记得保存",
		"data_updated_prompt": "表格数据已更新，可以保存文件",
		"column_type_conversion_complete": "列类型转换完成",
		"column_name_changed_to": "列名已更改为",
		"column_type_converted_to": "列类型已转换为",
		"column_edit_completed": "列编辑完成",
		"table_component_not_ready": "表格组件未准备好",
		"column_conversion_status": "第%s列已%s为%s类型",
		
		# 编辑对话框
		"edit_column": "编辑列",
		"column_index": "列索引",
		"current_type": "当前类型",
		"column_name": "列名称",
		"enter_new_column_name": "输入新的列名称",
		"cannot_modify_column_name": "此列的名称无法修改",
		"select_data_type_conversion": "选择数据类型转换",
		"conversion_preview": "转换预览",
		"conversion_preview_header": "转换预览 (前{count}项)",
		"select_type_to_preview": "选择类型查看转换预览...",
		"type_conversion_note": "注意: 类型转换将应用到该列的所有数据",
		"apply_changes": "应用更改",
		"force_convert": "强制转换",
		"cancel": "取消",
		"unknown": "未知",
		"double_click_edit_column_type": "双击编辑列类型: %s",
		

	},
	"en": {
		# 通用UI
		"tree_view": "Tree View",
		"table_view": "Table View",
		"tree_view_with_edit": "Tree View (Double-click to edit):",
		"excel_style_table_view": "Excel Style Table View:",
		"add_row": "Add Row",
		"key": "Key",
		"value": "Value",
		"value_header": "Value",
		"index": "Index",
		
		# 右键菜单
		"right_click_row_menu": "Right-click to show row operations menu",
		"add_new_row_before": "Add new row (before this row)",
		"add_new_row_after": "Add new row (after this row)",
		"copy_this_row": "Copy this row",
		"delete_this_row": "Delete this row",
		"enter_value": "Enter value...",

		# 单元格展开功能
		"double_click_to_expand": "Double-click to expand and view full content",
		"cell_content_viewer": "Cell Content Viewer - Row%s Column%s",
		"cell_position_info": "Position: Row %s, Column %s",
		"enter_cell_content": "Enter cell content...",
		"confirm": "Confirm",

		# 浮动窗口展开功能
		"content_expansion": "Content Expansion",
		"content_expansion_level": "Content Expansion (Level %d)",
		"table_mode": "📊 Table",
		"text_mode": "📝 Text",
		"content_placeholder": "Content will be displayed here...",
		"double_click_expand_tooltip": "🔍 Double-click to expand and view full content",
		"level_expansion": "%s Level %d Expansion",

		# 内联展开功能
		"collapse": "Collapse",
		"edit_mode": "Edit Mode",
		"view_mode": "View Mode",

		# 实时输入同步功能
		"realtime_input_sync_enabled": "Realtime Input Sync: Enabled",
		"realtime_input_sync_disabled": "Realtime Input Sync: Disabled",
		"realtime_input_detected": "Realtime Input Detected",
		"realtime_update_data": "Realtime Data Update",
		"floating_window_realtime_input": "Floating Window Realtime Input",

		# 性能优化功能
		"delayed_sync_timer_initialized": "Delayed sync timer initialized",
		"delayed_sync_requested": "Delayed sync requested",
		"delayed_sync_executing": "Executing delayed sync...",
		"delayed_sync_completed": "Delayed sync completed",
		"immediate_sync_executing": "Executing immediate sync...",
		"immediate_sync_completed": "Immediate sync completed",
		"sync_delay_set": "Sync delay time set to",
		"realtime_sync_delay_set": "Realtime sync delay time set to",

		# 内容展开检测
		"content_too_short": "Content too short, no expansion needed",
		"content_suitable_for_expansion": "Content suitable for expansion, expanding below",
		"content_suitable_for_floating_expansion": "Content suitable for expansion, level",
		"max_expansion_level_reached": "Maximum expansion level reached, cannot expand further",
		"child_expander_created": "Child expander created",
		"child_expander_closed": "Child expander closed",
		"source_info_recorded": "Child window source info recorded",
		"source_info_found": "Child window source info found",
		"source_info_not_found": "Warning: Child window source info not found",
		"cleanup_source_mapping": "Cleanup child window source mapping",

		# 表格标题和标签
		"table_header_key": "Key",
		"table_header_value": "Value",
		"table_header_index": "Index",
		"seconds": "seconds",

		# 弹出窗口按钮和标签
		"cancel_button": "❌ Cancel",
		"confirm_button": "✅ Confirm",
		"no_table_data": "Cannot parse as table data",
		"array_empty": "Array is empty",
		"input_value_placeholder": "Enter value...",

		# Excel表格中的硬编码文本
		"floating_window_mode_enabled": "Floating window mode enabled",
		"expansion_content_update_start": "=== Starting expansion content update ===",
		"new_content": "New content",
		"source_cell_validity": "source_cell validity",
		"cell_position": "Cell position: row",
		"column": "column",
		"direct_call_update_cell": "Direct call _update_cell_value",
		"cell_content_updated": "Cell content updated and signal sent",
		"error_source_cell_invalid": "Error: source_cell invalid",
		"error_no_source_cell": "Error: no source_cell in current_expanded_cell",
		"expansion_content_update_complete": "=== Expansion content update complete ===",
		"expander_closed": "Expander closed",
		"wait_next_frame": "Wait for next frame to ensure nodes are cleaned",
		"infer_column_type": "Infer column types",
		"infer_key_value_type": "Infer types for key-value mode",
		"infer_array_column_type": "Infer column types for objects in array",
		"infer_simple_array_type": "Infer types for simple array",
		"add_row_number_column": "Add row number column",
		"add_row_number": "Add row number",
		"deeper_blue": "Deeper blue",
		"add_gradient_effect": "Add gradient effect",
		"add_type_icon": "Add type icon to column header",
		"edit_icon": " ✎",
		"data_column_double_click": "Add double-click edit for data columns (non-row number)",
		"add_mouse_detection": "Add mouse detection for headers",
		"gray_color": "Gray color, different from normal headers",
		"hover_style": "Hover style",
		"add_right_click_menu": "Add right-click menu functionality",
		"normal_state_style": "Normal state style",
		"focus_state_style": "Focus state style",
		"add_shadow_effect": "Add shadow effect",
		"hover_state_style": "Hover state style",
		"font_style": "Font style",
		"add_expansion_tooltip": "Add expansion tooltip for cells",
		"realtime_text_change": "Handle real-time cell text changes",
		"detected_double_click": "Detected double-click event, row",
		"cell_content": "Cell content",
		"content_length": "Content length",
		"after_execution": " after execution",
		"delayed_sync_mode": "Delayed sync mode",
		"new_value_default": "New value",
		"smart_conversion_arrow": "Smart conversion → ",
		"force_conversion_arrow": "Force conversion → ",
		"floating_window_text_realtime_input": "Floating window text real-time input",

		# 单元格展开功能中的新增硬编码文本
		"current_expanded_cell": "current_expanded_cell",
		"update_before": "Before update",
		"update_after": "After update",
		"cell_position_row": "Cell position: row",
		"column_text": "column",
		"signal_sent_backup": "Also send signal as backup",
		"cell_content_updated_signal": "Cell content updated and signal sent",
		"detected_double_click_row": "Detected double-click event, row",
		"cell_content_text": "Cell content",
		"content_length_text": "Content length",
		"release_focus_prevent_edit": "Temporarily release focus to prevent edit mode interference",
		"expand_cell_content_floating": "Expand cell content to floating window, row",
		"save_expanded_cell_info": "Save current expanded cell information",
		"create_floating_expander": "Create floating expander window",
		"connect_signals": "Connect signals",
		"setup_expansion_info": "Setup expansion information",
		"set_window_title": "Set window title",
		"cell_content_title": "📋 Cell Content - Row%d Column%d (%s)",
		"cell_expansion_title": "Cell Expansion - Row%d Column%d",
		"realtime_update_object_collection": "Real-time update object collection",
		"realtime_update_key_value": "Real-time update key-value",
		"realtime_update_simple_array": "Real-time update simple array",
		"realtime_update_object_array": "Real-time update object array",

		# 硬编码文本修复
		"key_value_pair_mode": "Key-value pair mode",
		"value_column": "Value column",
		"key_column": "Key column",
		"check_all_values_boolean": "Check if all values are boolean type",
		"check_all_values_number": "Check if all values are number type",
		"infer_simple_values_type": "Infer simple values list type",
		"key_value_pair_operations": "Key-value pair mode row operations",
		"insert_new_key_value_pair": "Insert new key-value pair",
		"add_at_end": "Add new key-value pair at end",
		"copy_key_value_pair": "Copy key-value pair",
		"delete_key_value_pair": "Delete key-value pair",
		"add_new_value_to_array": "Added new value to array end",
		"insert_new_value_at_index": "Inserted new value at index",
		"copy_array_element": "Copied array element",
		"delete_array_element": "Deleted array element",
		"object_collection_mode_update_keys": "Object collection mode: update keys in all objects",
		"object_array_mode_update_keys": "Object array mode: update keys in all objects in array",
		"generate_unique_key_name": "Generate unique key name",
		"update_key_column": "Update key",
		"update_value_column": "Update value",
		"simple_array_value_column": "Simple array value column",
		"keyboard_navigation": "Handle keyboard navigation",
		"shortcut_key_save": "Detected shortcut key save",
		"from_index": "from index",
		"to": "to",
		"row_number_header": "Row #",
		
		# 状态消息
		"data_modified_save_reminder": "Data has been modified, remember to save",
		"data_updated_prompt": "Table data updated, ready to save file",
		"column_type_conversion_complete": "Column type conversion completed",
		"column_name_changed_to": "Column name changed to",
		"column_type_converted_to": "Column type converted to",
		"column_edit_completed": "Column editing completed",
		"table_component_not_ready": "Table component not ready",
		"column_conversion_status": "Column %s has been %s to %s type",
		
		# 编辑对话框
		"edit_column": "Edit Column",
		"column_index": "Column Index",
		"current_type": "Current Type",
		"column_name": "Column Name",
		"enter_new_column_name": "Enter new column name",
		"cannot_modify_column_name": "This column's name cannot be modified",
		"select_data_type_conversion": "Select data type conversion",
		"conversion_preview": "Conversion Preview",
		"select_type_to_preview": "Select type to view conversion preview...",
		"type_conversion_note": "Note: Type conversion will apply to all data in this column",
		"apply_changes": "Apply Changes",
		"force_convert": "Force Convert",
		"cancel": "Cancel",
		"unknown": "Unknown",
		"double_click_edit_column_type": "Double-click to edit column type: %s",
		
		# 数据类型
		"string_type": "String",
		"number_type": "Number",
		"boolean_type": "Boolean",
		"type_string": "String",
		"type_number": "Number", 
		"type_boolean": "Boolean",
		"type_integer": "Integer",
		"type_float": "Float",
		"type_other": "Other",
		"string_type_full": "String (String)",
		"number_type_full": "Number (Number)",
		"boolean_type_full": "Boolean (Boolean)",
		
		# 转换模式
		"smart_conversion": "Smart Conversion",
		"force_conversion": "Force Conversion",
		"smart_conversion_result": "Smart Conversion",
		"force_conversion_result": "Force Conversion",
		
		# 错误信息
		"operation_failed": "The following operations failed",
		"operation_error": "Operation Error",
		"column_name_cannot_be_empty": "Column name cannot be empty",
		"column_name_exists_or_invalid": "Column name already exists or is invalid",
		"cannot_rename_column": "Cannot rename column",
		"invalid_column_index": "Invalid column index",
		"no_convertible_data_found": "No convertible data found",
		"cannot_get_preview": "Cannot get preview",
		
		# 删除确认
		"confirm_delete": "Confirm Delete",
		"confirm_delete_row": "Are you sure you want to delete row %d?\nThis action cannot be undone.",
		
		# 类型转换预览
		"conversion_preview_header": "Conversion Preview (first {count} items)",
		
		# 调试信息
		"start_conversion": "Start %s column %s (%s) to type %s",
		"conversion_complete": "Column type conversion completed",
		"convert_column": "Convert column: %s to type: %s (%s)",
		"convert_object": "Convert %s.%s: %s -> %s",
		"convert_value": "Convert value %s: %s -> %s",
		"convert_array": "Convert array[%s]: %s -> %s"
	}
}

# 设置语言
static func set_language(language: String):
	if language in translations:
		var old_language = current_language
		current_language = language
		print("Language set to: ", language)

		# 通知所有监听器语言已变化
		if old_language != current_language:
			_notify_language_change(current_language)

# 添加语言变化监听器
static func add_language_change_listener(callback: Callable):
	if callback not in language_change_listeners:
		language_change_listeners.append(callback)

# 移除语言变化监听器
static func remove_language_change_listener(callback: Callable):
	if callback in language_change_listeners:
		language_change_listeners.erase(callback)

# 通知语言变化
static func _notify_language_change(new_language: String):
	for callback in language_change_listeners:
		if callback.is_valid():
			callback.call(new_language)
		else:
			# 移除无效的回调
			language_change_listeners.erase(callback)

# 获取翻译文本
static func get_text(key: String) -> String:
	if current_language in translations and key in translations[current_language]:
		return translations[current_language][key]
	print("Translation missing for key: ", key, " in language: ", current_language)
	return key  # 如果没有找到翻译，返回原始key

# 获取当前语言
static func get_current_language() -> String:
	return current_language
