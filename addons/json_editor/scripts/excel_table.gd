@tool
extends Control
class_name ExcelTable

# 引入翻译管理器
const TranslationManager = preload("res://addons/json_editor/scripts/translation_manager.gd")
# 引入单元格展开器
const CellExpander = preload("res://addons/json_editor/scripts/cell_expander.gd")
# 引入通用展开器
const UniversalCellExpander = preload("res://addons/json_editor/scripts/universal_cell_expander.gd")

signal data_changed(new_data: Variant)
signal cell_selected(row: int, column: int)
signal column_type_edit_requested(column_index: int, column_name: String)
signal row_add_requested(row_index: int)
signal row_copy_requested(row_index: int)
signal row_delete_requested(row_index: int)

var grid_container: GridContainer
var scroll_container: ScrollContainer
var main_container: VBoxContainer  # 主容器，包含表格和展开区域
var universal_expander: UniversalCellExpander  # 通用展开器
var current_expanded_cell: Dictionary = {}  # 当前展开的单元格信息
var current_data: Variant
var headers: Array[String] = []
var rows_data: Array[Array] = []
var cell_inputs: Array[Array] = []
var column_types: Array[int] = []  # 存储每列的数据类型 0=String, 1=Number, 2=Boolean
var cell_expansion_manager: CellExpander.CellExpansionManager  # 单元格展开管理器

# 性能优化：延迟同步机制
var sync_timer: Timer = null
var pending_sync: bool = false
var sync_delay: float = 0.5  # 500ms延迟，避免频繁更新造成卡顿
var enable_delayed_sync: bool = true  # 是否启用延迟同步（可配置）

# 实时输入同步
var enable_realtime_sync: bool = true  # 是否启用实时输入同步
var realtime_sync_delay: float = 0.2   # 实时同步延迟（200ms，比确认同步更快）

func _ready():
	custom_minimum_size = Vector2(600, 300)
	_create_ui()
	# 初始化单元格展开管理器
	cell_expansion_manager = CellExpander.create_expansion_manager(self)

	# 初始化延迟同步定时器
	_setup_sync_timer()

func _setup_sync_timer():
	"""设置延迟同步定时器，优化性能"""
	sync_timer = Timer.new()
	sync_timer.wait_time = sync_delay
	sync_timer.one_shot = true
	sync_timer.timeout.connect(_perform_delayed_sync)
	add_child(sync_timer)
	print(TranslationManager.get_text("delayed_sync_timer_initialized"), ", ", TranslationManager.get_text("sync_delay_set"), ":", sync_delay, TranslationManager.get_text("seconds"))

func _perform_delayed_sync():
	"""执行延迟同步，避免频繁更新"""
	if pending_sync:
		print(TranslationManager.get_text("delayed_sync_executing"))
		_sync_data_back()
		pending_sync = false
		print(TranslationManager.get_text("delayed_sync_completed"))

func _request_sync(immediate: bool = false):
	"""请求数据同步（支持立即同步和延迟同步）"""
	if immediate or not enable_delayed_sync:
		# 立即同步（用于重要操作或禁用延迟同步时）
		print(TranslationManager.get_text("immediate_sync_executing"))
		if sync_timer:
			sync_timer.stop()
		_sync_data_back()
		pending_sync = false
		print(TranslationManager.get_text("immediate_sync_completed"))
	else:
		# 延迟同步（用于编辑操作，避免卡顿）
		if not pending_sync:
			pending_sync = true
			print(TranslationManager.get_text("delayed_sync_requested"), ", ", sync_delay, TranslationManager.get_text("seconds"), TranslationManager.get_text("after_execution"))

		# 重启定时器（如果有新的编辑，会重新计时）
		if sync_timer:
			sync_timer.stop()
			sync_timer.start()

func force_sync():
	"""强制立即同步（公共接口）"""
	_request_sync(true)

# 性能配置接口
func set_sync_delay(delay: float):
	"""设置同步延迟时间（秒）"""
	sync_delay = delay
	if sync_timer:
		sync_timer.wait_time = delay
	print(TranslationManager.get_text("sync_delay_set"), ":", delay, TranslationManager.get_text("seconds"))

func enable_delayed_sync_mode(enabled: bool):
	"""启用或禁用延迟同步模式"""
	enable_delayed_sync = enabled
	print(TranslationManager.get_text("delayed_sync_mode"), ":", TranslationManager.get_text("realtime_input_sync_enabled") if enabled else TranslationManager.get_text("realtime_input_sync_disabled"))

func get_performance_info() -> Dictionary:
	"""获取性能相关信息"""
	return {
		"sync_delay": sync_delay,
		"delayed_sync_enabled": enable_delayed_sync,
		"realtime_sync_enabled": enable_realtime_sync,
		"realtime_sync_delay": realtime_sync_delay,
		"pending_sync": pending_sync,
		"rows_count": rows_data.size(),
		"total_cells": rows_data.size() * (headers.size() if headers.size() > 0 else 0)
	}

func _request_realtime_sync(row: int, col: int, text: String):
	"""请求实时同步（输入过程中的快速更新）"""
	if not enable_realtime_sync:
		return

	# 立即更新底层数据（不触发完整同步）
	if row < rows_data.size() and col < rows_data[row].size():
		var old_value = rows_data[row][col]
		rows_data[row][col] = text
		print(TranslationManager.get_text("realtime_update_data"), ": [", row, ",", col, "] =", text.substr(0, 20) + "...")

		# 只有当值真正改变时才触发同步
		if old_value != text:
			# 立即更新current_data以实现真正的实时同步
			_update_current_data_realtime(row, col, text)

			# 发出实时数据变化信号
			data_changed.emit(current_data)

			# 使用延迟同步机制作为备份，避免过于频繁的完整同步
			_request_sync()

func _update_current_data_realtime(row: int, col: int, text: String):
	"""实时更新current_data，避免等待延迟同步"""
	if not current_data:
		return

	match typeof(current_data):
		TYPE_DICTIONARY:
			_update_dictionary_realtime(row, col, text)
		TYPE_ARRAY:
			_update_array_realtime(row, col, text)

func _update_dictionary_realtime(row: int, col: int, text: String):
	"""实时更新字典类型的current_data"""
	if headers.size() > 0 and headers[0] == "ID":
		# 对象集合模式
		if row < rows_data.size() and rows_data[row].size() > 0:
			var item_id = rows_data[row][0]
			if current_data.has(item_id) and col > 0 and col < headers.size():
				var column_name = headers[col]
				var obj = current_data[item_id]
				if typeof(obj) == TYPE_DICTIONARY:
					var parsed_value = _parse_value(text)
					obj[column_name] = parsed_value
					print(TranslationManager.get_text("realtime_update_object_collection"), ": ", item_id, ".", column_name, " = ", parsed_value)
	else:
		# 键值对模式
		if row < rows_data.size() and rows_data[row].size() >= 2:
			var key = rows_data[row][0]
			if col == 1:  # 值列
				var parsed_value = _parse_value(text)
				current_data[key] = parsed_value
				print(TranslationManager.get_text("realtime_update_key_value"), ": ", key, " = ", parsed_value)

func _update_array_realtime(row: int, col: int, text: String):
	"""实时更新数组类型的current_data"""
	if headers.size() == 2 and headers[0] == TranslationManager.get_text("index"):
		# 简单数组模式
		if col == 1 and row < current_data.size():  # 值列
			var parsed_value = _parse_value(text)
			current_data[row] = parsed_value
			print(TranslationManager.get_text("realtime_update_simple_array"), "[", row, "] = ", parsed_value)
	else:
		# 对象数组模式
		if row < current_data.size() and col < headers.size():
			var item = current_data[row]
			if typeof(item) == TYPE_DICTIONARY:
				var column_name = headers[col]
				var parsed_value = _parse_value(text)
				item[column_name] = parsed_value
				print(TranslationManager.get_text("realtime_update_object_array"), "[", row, "].", column_name, " = ", parsed_value)

# 实时同步配置接口
func enable_realtime_sync_mode(enabled: bool):
	"""启用或禁用实时输入同步"""
	enable_realtime_sync = enabled
	print(TranslationManager.get_text("realtime_input_sync_enabled") if enabled else TranslationManager.get_text("realtime_input_sync_disabled"))

func set_realtime_sync_delay(delay: float):
	"""设置实时同步延迟时间"""
	realtime_sync_delay = delay
	print(TranslationManager.get_text("realtime_sync_delay_set"), ":", delay, TranslationManager.get_text("seconds"))

func _create_ui():
	# 设置整体背景样式
	var bg_style = StyleBoxFlat.new()
	bg_style.bg_color = Color("#FAFBFC")
	bg_style.border_width_left = 1
	bg_style.border_width_top = 1
	bg_style.border_width_right = 1
	bg_style.border_width_bottom = 1
	bg_style.border_color = Color("#E1E5E9")
	bg_style.corner_radius_top_left = 6
	bg_style.corner_radius_top_right = 6
	bg_style.corner_radius_bottom_left = 6
	bg_style.corner_radius_bottom_right = 6
	add_theme_stylebox_override("panel", bg_style)

	# 创建主容器
	main_container = VBoxContainer.new()
	main_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	main_container.add_theme_constant_override("margin_left", 8)
	main_container.add_theme_constant_override("margin_top", 8)
	main_container.add_theme_constant_override("margin_right", 8)
	main_container.add_theme_constant_override("margin_bottom", 8)
	add_child(main_container)

	# 创建表格滚动容器
	scroll_container = ScrollContainer.new()
	scroll_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_container.add_child(scroll_container)

	grid_container = GridContainer.new()
	grid_container.add_theme_constant_override("h_separation", 0)
	grid_container.add_theme_constant_override("v_separation", 0)
	scroll_container.add_child(grid_container)

	# 创建展开区域
	_create_expansion_area()

func _create_expansion_area():
	"""创建单元格内容展开区域"""
	# 浮动窗口模式下不需要预创建展开器
	print(TranslationManager.get_text("floating_window_mode_enabled"))

func _on_expansion_content_updated(new_content: String):
	"""处理展开器内容更新"""
	print(TranslationManager.get_text("expansion_content_update_start"))
	print(TranslationManager.get_text("new_content"), ":", new_content.substr(0, 100) + "...")
	print(TranslationManager.get_text("current_expanded_cell"), ":", current_expanded_cell)

	if current_expanded_cell.has("source_cell"):
		var source_cell = current_expanded_cell["source_cell"] as LineEdit
		print(TranslationManager.get_text("source_cell_validity"), ":", source_cell != null and is_instance_valid(source_cell))

		if source_cell and is_instance_valid(source_cell):
			print(TranslationManager.get_text("update_before"), " source_cell.text:", source_cell.text)
			source_cell.text = new_content
			print(TranslationManager.get_text("update_after"), " source_cell.text:", source_cell.text)

			# 获取单元格位置信息
			var row = current_expanded_cell.get("row", -1)
			var col = current_expanded_cell.get("col", -1)
			print(TranslationManager.get_text("cell_position_row"), row, TranslationManager.get_text("column_text"), col)

			# 直接调用更新函数而不依赖信号
			if row >= 0 and col >= 0:
				print(TranslationManager.get_text("direct_call_update_cell"))
				_update_cell_value(row, col, new_content)

			# 同时也发送信号作为备用
			source_cell.text_submitted.emit(new_content)
			print(TranslationManager.get_text("cell_content_updated_signal"))
		else:
			print(TranslationManager.get_text("error_source_cell_invalid"))
	else:
		print(TranslationManager.get_text("error_no_source_cell"))

	print(TranslationManager.get_text("expansion_content_update_complete"))

func _on_expansion_closed():
	"""处理展开器关闭"""
	current_expanded_cell.clear()
	print(TranslationManager.get_text("expander_closed"))

func setup_data(data: Variant):
	current_data = data
	await _clear_table()
	_analyze_data(data)
	_build_table()

func _clear_table():
	if grid_container:
		for child in grid_container.get_children():
			child.queue_free()
		# 等待下一帧确保节点被清理
		await get_tree().process_frame
	headers.clear()
	rows_data.clear()
	cell_inputs.clear()
	column_types.clear()

func _analyze_data(data: Variant):
	match typeof(data):
		TYPE_DICTIONARY:
			_analyze_dictionary(data)
		TYPE_ARRAY:
			_analyze_array(data)
		_:
			headers.clear()
			headers.append(TranslationManager.get_text("value_header"))
			rows_data.clear()
			var row: Array[String] = []
			row.append(str(data))
			rows_data.append(row)

func _analyze_dictionary(data: Dictionary):
	var all_objects = true
	for value in data.values():
		if typeof(value) != TYPE_DICTIONARY:
			all_objects = false
			break

	if all_objects and data.size() > 0:
		var all_keys = {"ID": true}
		for obj in data.values():
			for key in obj.keys():
				all_keys[key] = true

		var keys_array = all_keys.keys()
		headers.clear()
		for key in keys_array:
			headers.append(str(key))
		headers.sort()

		# 推断每列的类型
		column_types.clear()
		for i in range(headers.size()):
			var column_type = _infer_column_type(data, headers[i], i == 0)
			column_types.append(column_type)

		rows_data.clear()
		for item_id in data.keys():
			var row: Array[String] = []
			row.append(str(item_id))
			var obj = data[item_id]
			for i in range(1, headers.size()):
				var key = headers[i]
				if obj.has(key):
					row.append(str(obj[key]))
				else:
					row.append("")
			rows_data.append(row)
	else:
		headers.clear()
		headers.append(TranslationManager.get_text("key"))
		headers.append(TranslationManager.get_text("value"))

		# 为键值对模式推断类型
		column_types.clear()
		column_types.append(0)  # 键总是字符串
		column_types.append(_infer_simple_values_type(data.values()))

		rows_data.clear()
		for key in data.keys():
			var row: Array[String] = []
			row.append(str(key))
			row.append(str(data[key]))
			rows_data.append(row)

func _analyze_array(data: Array):
	if data.is_empty():
		headers.clear()
		headers.append(TranslationManager.get_text("value_header"))
		rows_data.clear()
		return

	var first_item = data[0]
	if typeof(first_item) == TYPE_DICTIONARY:
		var all_keys = {}
		for item in data:
			if typeof(item) == TYPE_DICTIONARY:
				for key in item.keys():
					all_keys[key] = true

		var keys_array = all_keys.keys()
		headers.clear()
		for key in keys_array:
			headers.append(str(key))
		headers.sort()

		# 为数组中的对象推断列类型
		column_types.clear()
		for i in range(headers.size()):
			var column_type = _infer_array_column_type(data, headers[i])
			column_types.append(column_type)

		rows_data.clear()
		for item in data:
			var row: Array[String] = []
			for header in headers:
				if typeof(item) == TYPE_DICTIONARY and item.has(header):
					row.append(str(item[header]))
				else:
					row.append("")
			rows_data.append(row)
	else:
		headers.clear()
		headers.append(TranslationManager.get_text("index"))
		headers.append(TranslationManager.get_text("value"))

		# 为简单数组推断类型
		column_types.clear()
		column_types.append(0)  # 索引总是字符串
		column_types.append(_infer_simple_values_type(data))

		rows_data.clear()
		for i in range(data.size()):
			var row: Array[String] = []
			row.append(str(i))
			row.append(str(data[i]))
			rows_data.append(row)

func _build_table():
	if headers.is_empty():
		return

	if not grid_container:
		_create_ui()

	# 添加行号列
	grid_container.columns = headers.size() + 1

	# 创建行号标题
	var row_header = _create_row_number_header()
	grid_container.add_child(row_header)

	# 创建其他标题
	for i in range(headers.size()):
		var header = headers[i]
		var header_cell = _create_header_cell(header, i)
		grid_container.add_child(header_cell)

	cell_inputs.clear()
	for row_idx in range(rows_data.size()):
		# 添加行号
		var row_number_cell = _create_row_number_cell(row_idx)
		grid_container.add_child(row_number_cell)

		var row_cells: Array[LineEdit] = []
		for col_idx in range(headers.size()):
			var value = ""
			if col_idx < rows_data[row_idx].size():
				value = rows_data[row_idx][col_idx]

			var cell = _create_data_cell(value, row_idx, col_idx)
			grid_container.add_child(cell)
			row_cells.append(cell)

		cell_inputs.append(row_cells)

func _create_header_cell(text: String, column_index: int = -1) -> Panel:
	var panel = Panel.new()
	panel.custom_minimum_size = Vector2(120, 35)

	var style = StyleBoxFlat.new()
	style.bg_color = Color("#2E5BBA")  # 更深的蓝色
	style.border_width_bottom = 2
	style.border_width_right = 1
	style.border_width_top = 1
	style.border_width_left = 1
	style.border_color = Color("#FFFFFF")
	style.corner_radius_top_left = 3
	style.corner_radius_top_right = 3
	# 添加渐变效果
	style.bg_color = Color("#4A90E2")
	panel.add_theme_stylebox_override("panel", style)

	# 如果是数据列，在标题上添加编辑图标
	if column_index >= 0:
		var hbox = HBoxContainer.new()
		hbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		hbox.alignment = BoxContainer.ALIGNMENT_CENTER
		panel.add_child(hbox)

		var label = Label.new()
		# 添加类型图标到列标题
		var type_icon = _get_type_icon(column_index)
		label.text = text + type_icon
		label.add_theme_color_override("font_color", Color.WHITE)
		label.add_theme_font_size_override("font_size", 13)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		hbox.add_child(label)

		var edit_icon = Label.new()
		edit_icon.text = " ✎"  # 编辑图标
		edit_icon.add_theme_color_override("font_color", Color("#DDDDDD"))
		edit_icon.add_theme_font_size_override("font_size", 12)
		edit_icon.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		edit_icon.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		hbox.add_child(edit_icon)
	else:
		var label = Label.new()
		label.text = text
		label.add_theme_color_override("font_color", Color.WHITE)
		label.add_theme_font_size_override("font_size", 13)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		label.add_theme_stylebox_override("normal", StyleBoxEmpty.new())
		panel.add_child(label)

	# 如果是数据列（非行号列），添加双击编辑功能
	if column_index >= 0:
		panel.gui_input.connect(_on_header_input.bind(column_index, text))
		# 添加鼠标悬停提示
		panel.tooltip_text = TranslationManager.get_text("double_click_edit_column_type") % text

		# 悬停样式
		var hover_style = StyleBoxFlat.new()
		hover_style.bg_color = Color("#5BA3F5")  # 稍亮的蓝色
		hover_style.border_width_bottom = 2
		hover_style.border_width_right = 1
		hover_style.border_width_top = 1
		hover_style.border_width_left = 1
		hover_style.border_color = Color("#FFFFFF")
		hover_style.corner_radius_top_left = 3
		hover_style.corner_radius_top_right = 3

		# 为标题添加鼠标检测
		panel.mouse_entered.connect(_on_header_mouse_entered.bind(panel, hover_style))
		panel.mouse_exited.connect(_on_header_mouse_exited.bind(panel, style))

	return panel

func _create_row_number_header() -> Panel:
	var panel = Panel.new()
	panel.custom_minimum_size = Vector2(50, 35)

	var style = StyleBoxFlat.new()
	style.bg_color = Color("#6B7280")  # 灰色，区别于普通标题
	style.border_width_bottom = 2
	style.border_width_right = 2
	style.border_width_top = 1
	style.border_width_left = 1
	style.border_color = Color("#FFFFFF")
	style.corner_radius_top_left = 3
	panel.add_theme_stylebox_override("panel", style)

	var label = Label.new()
	label.text = "#"
	label.add_theme_color_override("font_color", Color.WHITE)
	label.add_theme_font_size_override("font_size", 12)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

	panel.add_child(label)
	return panel

func _create_row_number_cell(row_number: int) -> Panel:
	var panel = Panel.new()
	panel.custom_minimum_size = Vector2(50, 30)

	var style = StyleBoxFlat.new()
	style.bg_color = Color("#F3F4F6")
	style.border_width_bottom = 1
	style.border_width_right = 2
	style.border_width_top = 1
	style.border_width_left = 1
	style.border_color = Color("#E1E5E9")
	panel.add_theme_stylebox_override("panel", style)

	# 悬停样式
	var hover_style = StyleBoxFlat.new()
	hover_style.bg_color = Color("#E5E7EB")
	hover_style.border_width_bottom = 1
	hover_style.border_width_right = 2
	hover_style.border_width_top = 1
	hover_style.border_width_left = 1
	hover_style.border_color = Color("#D1D5DB")

	var label = Label.new()
	label.text = str(row_number + 1)
	label.add_theme_color_override("font_color", Color("#6B7280"))
	label.add_theme_font_size_override("font_size", 11)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

	panel.add_child(label)

	# 添加右键菜单功能
	panel.gui_input.connect(_on_row_number_input.bind(row_number))
	panel.mouse_entered.connect(_on_row_number_mouse_entered.bind(panel, hover_style))
	panel.mouse_exited.connect(_on_row_number_mouse_exited.bind(panel, style))
	panel.tooltip_text = TranslationManager.get_text("right_click_row_menu")

	return panel

func _create_data_cell(value: String, row: int, col: int) -> LineEdit:
	var line_edit = LineEdit.new()
	line_edit.text = value
	line_edit.custom_minimum_size = Vector2(120, 30)
	line_edit.placeholder_text = TranslationManager.get_text("enter_value")

	# 普通状态样式
	var normal_style = StyleBoxFlat.new()
	normal_style.bg_color = Color.WHITE if row % 2 == 0 else Color("#F8F9FA")
	normal_style.border_width_bottom = 1
	normal_style.border_width_right = 1
	normal_style.border_width_top = 1
	normal_style.border_width_left = 1
	normal_style.border_color = Color("#E1E5E9")
	normal_style.content_margin_left = 8
	normal_style.content_margin_right = 8
	normal_style.content_margin_top = 4
	normal_style.content_margin_bottom = 4
	line_edit.add_theme_stylebox_override("normal", normal_style)

	# 焦点状态样式
	var focus_style = StyleBoxFlat.new()
	focus_style.bg_color = Color("#FFFFFF")
	focus_style.border_width_left = 2
	focus_style.border_width_top = 2
	focus_style.border_width_right = 2
	focus_style.border_width_bottom = 2
	focus_style.border_color = Color("#4A90E2")
	focus_style.content_margin_left = 8
	focus_style.content_margin_right = 8
	focus_style.content_margin_top = 4
	focus_style.content_margin_bottom = 4
	# 添加阴影效果
	var shadow_color = Color("#4A90E2")
	shadow_color.a = 0.3
	focus_style.shadow_color = shadow_color
	focus_style.shadow_size = 2
	line_edit.add_theme_stylebox_override("focus", focus_style)

	# 悬停状态样式
	var hover_style = StyleBoxFlat.new()
	hover_style.bg_color = Color("#F0F7FF")
	hover_style.border_width_bottom = 1
	hover_style.border_width_right = 1
	hover_style.border_width_top = 1
	hover_style.border_width_left = 1
	hover_style.border_color = Color("#B8D4F0")
	hover_style.content_margin_left = 8
	hover_style.content_margin_right = 8
	hover_style.content_margin_top = 4
	hover_style.content_margin_bottom = 4
	line_edit.add_theme_stylebox_override("hover", hover_style)

	# 字体样式
	line_edit.add_theme_font_size_override("font_size", 12)
	line_edit.add_theme_color_override("font_color", Color("#333333"))
	line_edit.add_theme_color_override("font_placeholder_color", Color("#999999"))

	line_edit.text_submitted.connect(func(text: String): _on_cell_text_submitted(text, row, col))
	line_edit.text_changed.connect(func(text: String): _on_cell_text_changed(text, row, col))
	line_edit.focus_entered.connect(_on_cell_focus_entered.bind(row, col))
	line_edit.gui_input.connect(_on_cell_input.bind(row, col, line_edit))

	# 为单元格添加展开提示到tooltip
	if cell_expansion_manager:
		cell_expansion_manager.setup_cell_expansion(line_edit, row, col)

	return line_edit

func _on_cell_focus_entered(row: int, col: int):
	cell_selected.emit(row, col)

func _on_cell_text_changed(text: String, row: int, col: int):
	"""处理单元格文本实时变化"""
	if enable_realtime_sync:
		print(TranslationManager.get_text("realtime_input_detected"), ": 行", row, " 列", col, " 内容:", text.substr(0, 30) + "...")
		# 使用更短的延迟进行实时同步
		_request_realtime_sync(row, col, text)

func _on_cell_text_submitted(text: String, row: int, col: int):
	print("=== _on_cell_text_submitted 被调用 ===")
	print("参数: text=", text.substr(0, 50) + "...", " row=", row, " col=", col)
	_update_cell_value(row, col, text)
	_move_to_next_cell(row, col)
	print("=== _on_cell_text_submitted 处理完成 ===")

func _on_cell_input(event: InputEvent, row: int, col: int, line_edit: LineEdit):
	# 处理双击展开功能
	if event is InputEventMouseButton:
		var mouse_event = event as InputEventMouseButton
		if mouse_event.pressed and mouse_event.button_index == MOUSE_BUTTON_LEFT:
			if mouse_event.double_click:
				print(TranslationManager.get_text("detected_double_click_row"), ":", row, " ", TranslationManager.get_text("column_text"), ":", col)
				# 检查内容是否适合展开
				var content = line_edit.text
				print(TranslationManager.get_text("cell_content_text"), ":", content)
				print(TranslationManager.get_text("content_length_text"), ":", content.length())
				if UniversalCellExpander.is_content_expandable(content):
					print(TranslationManager.get_text("content_suitable_for_expansion"))
					# 暂时释放焦点，防止编辑模式干扰
					line_edit.release_focus()
					_expand_cell_content(row, col, content, line_edit)
					get_viewport().set_input_as_handled()
					return
				else:
					print(TranslationManager.get_text("content_too_short"))

	if event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_TAB:
				_update_cell_value(row, col, line_edit.text)
				if event.shift_pressed:
					_move_to_previous_cell(row, col)
				else:
					_move_to_next_cell(row, col)
				get_viewport().set_input_as_handled()
			KEY_ENTER:
				_update_cell_value(row, col, line_edit.text)
				_move_to_next_row(row, col)
				get_viewport().set_input_as_handled()

func _update_cell_value(row: int, col: int, value: String):
	print("=== _update_cell_value 被调用 ===")
	print("参数: row=", row, " col=", col, " value=", value.substr(0, 50) + "...")
	print("rows_data.size()=", rows_data.size())

	if row < rows_data.size():
		print("rows_data[", row, "].size()=", rows_data[row].size())
		if col < rows_data[row].size():
			print("更新前 rows_data[", row, "][", col, "]=", rows_data[row][col])
			rows_data[row][col] = value
			print("更新后 rows_data[", row, "][", col, "]=", rows_data[row][col])

			# 同时更新UI中的单元格显示
			if row < cell_inputs.size() and col < cell_inputs[row].size():
				var ui_cell = cell_inputs[row][col]
				if ui_cell and is_instance_valid(ui_cell):
					print("同时更新UI单元格显示")
					ui_cell.text = value

			# 性能优化：使用延迟同步而不是立即同步
			_request_sync()
		else:
			print("错误：列索引超出范围 col=", col, " >= ", rows_data[row].size())
	else:
		print("错误：行索引超出范围 row=", row, " >= ", rows_data.size())

	print("=== _update_cell_value 处理完成 ===")

func _sync_data_back():
	match typeof(current_data):
		TYPE_DICTIONARY:
			_sync_to_dictionary()
		TYPE_ARRAY:
			_sync_to_array()

	# 发出数据变化信号，这会触发主编辑器的保存逻辑
	data_changed.emit(current_data)

	# 在控制台输出提示，表示数据已更新
	print(TranslationManager.get_text("data_updated_prompt"))

func _sync_to_dictionary():
	if headers.size() > 0 and headers[0] == "ID":
		var new_data = {}
		for row in rows_data:
			if row.size() > 0:
				var item_id = row[0]
				var obj = {}
				for i in range(1, min(headers.size(), row.size())):
					var column_type = column_types[i] if i < column_types.size() else 0
					obj[headers[i]] = _parse_value_with_type(row[i], column_type)
				new_data[item_id] = obj
		current_data = new_data
	else:
		var new_data = {}
		for row in rows_data:
			if row.size() >= 2:
				var column_type = column_types[1] if column_types.size() > 1 else 0
				new_data[row[0]] = _parse_value_with_type(row[1], column_type)
		current_data = new_data

func _sync_to_array():
	if headers.size() == 2 and headers[0] == TranslationManager.get_text("index"):
		var new_array = []
		for row in rows_data:
			if row.size() > 1:
				var column_type = column_types[1] if column_types.size() > 1 else 0
				new_array.append(_parse_value_with_type(row[1], column_type))
		current_data = new_array
	else:
		var new_array = []
		for row in rows_data:
			var obj = {}
			for i in range(min(headers.size(), row.size())):
				var column_type = column_types[i] if i < column_types.size() else 0
				obj[headers[i]] = _parse_value_with_type(row[i], column_type)
			new_array.append(obj)
		current_data = new_array

func _parse_value(value: String) -> Variant:
	if value.is_valid_int():
		return value.to_int()
	elif value.is_valid_float():
		return value.to_float()
	elif value.to_lower() in ["true", "false"]:
		return value.to_lower() == "true"
	else:
		return value

func _parse_value_with_type(value: String, expected_type: int) -> Variant:
	"""根据指定的类型解析值"""
	match expected_type:
		0: # 字符串类型
			return value  # 保持为字符串，不进行类型转换
		1: # 数字类型
			if value.is_valid_int():
				return value.to_int()
			elif value.is_valid_float():
				return value.to_float()
			else:
				return 0  # 无法转换时返回0
		2: # 布尔类型
			var lower_value = value.to_lower()
			if lower_value in ["true", "1", "yes", "on"]:
				return true
			elif lower_value in ["false", "0", "no", "off"]:
				return false
			else:
				return value != ""  # 非空字符串为true
		_:
			return value  # 默认返回字符串

func _move_to_next_cell(row: int, col: int):
	var next_col = col + 1
	var next_row = row

	if next_col >= headers.size():
		next_col = 0
		next_row += 1

	if next_row < cell_inputs.size():
		_focus_cell(next_row, next_col)

func _move_to_previous_cell(row: int, col: int):
	var prev_col = col - 1
	var prev_row = row

	if prev_col < 0:
		prev_col = headers.size() - 1
		prev_row -= 1

	if prev_row >= 0:
		_focus_cell(prev_row, prev_col)

func _move_to_next_row(row: int, col: int):
	var next_row = row + 1
	if next_row < cell_inputs.size():
		_focus_cell(next_row, col)

func _focus_cell(row: int, col: int):
	if row < cell_inputs.size() and col < cell_inputs[row].size():
		cell_inputs[row][col].grab_focus()

func _on_header_input(event: InputEvent, column_index: int, column_name: String):
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_LEFT and event.double_click:
			# 直接发出列编辑请求信号，让主编辑器处理
			column_type_edit_requested.emit(column_index, column_name)

func _on_header_mouse_entered(panel: Panel, hover_style: StyleBoxFlat):
	panel.add_theme_stylebox_override("panel", hover_style)

func _on_header_mouse_exited(panel: Panel, normal_style: StyleBoxFlat):
	panel.add_theme_stylebox_override("panel", normal_style)

func convert_column_type(column_index: int, target_type: int, force_convert: bool = false) -> void:
	"""转换指定列的数据类型"""
	if column_index < 0 or column_index >= headers.size():
		print(TranslationManager.get_text("invalid_column_index") + ":", column_index)
		return

	var convert_mode = TranslationManager.get_text("force_conversion") if force_convert else TranslationManager.get_text("smart_conversion")
	print(TranslationManager.get_text("start_conversion") % [convert_mode, str(column_index), headers[column_index], str(target_type)])

	# 更新列类型记录
	if column_index < column_types.size():
		column_types[column_index] = target_type
	else:
		# 扩展数组到指定索引
		while column_types.size() <= column_index:
			column_types.append(0)
		column_types[column_index] = target_type

	# 直接修改current_data中的数据，而不是rows_data
	_convert_current_data_column(column_index, target_type, force_convert)

	# 重新分析数据并重建表格
	await _clear_table()
	_analyze_data(current_data)
	_build_table()

	# 触发数据变化信号，同步到主编辑器
	data_changed.emit(current_data)

	print(TranslationManager.get_text("conversion_complete"))

func _convert_value_type(value: String, target_type: int, force_convert: bool = false) -> Variant:
	"""转换单个值的类型"""
	match target_type:
		0: # 字符串
			return str(value)
		1: # 数字
			if value.is_valid_int():
				return value.to_int()
			elif value.is_valid_float():
				return value.to_float()
			else:
				if force_convert:
					# 强制转换：尝试提取数字，失败则为0
					var regex = RegEx.new()
					regex.compile(r"-?\d+\.?\d*")
					var result = regex.search(value)
					if result:
						var num_str = result.get_string()
						if num_str.is_valid_int():
							return num_str.to_int()
						elif num_str.is_valid_float():
							return num_str.to_float()
					return 0
				else:
					# 智能转换：无法转换则保持原值
					return value
		2: # 布尔值
			var lower_value = value.to_lower()
			if lower_value in ["true", "1", "yes", "on"]:
				return true
			elif lower_value in ["false", "0", "no", "off"]:
				return false
			else:
				if force_convert:
					# 强制转换：非空字符串为true
					return value != ""
				else:
					# 智能转换：无法识别则保持原值
					return value
		_:
			return value

func _convert_current_data_column(column_index: int, target_type: int, force_convert: bool = false) -> void:
	"""直接转换current_data中指定列的数据类型"""
	if column_index >= headers.size():
		return

	var column_name = headers[column_index]
	var convert_mode = TranslationManager.get_text("force_conversion") if force_convert else TranslationManager.get_text("smart_conversion")
	print("转换列: ", column_name, " 到类型: ", target_type, " (", convert_mode, ")")

	match typeof(current_data):
		TYPE_DICTIONARY:
			_convert_dictionary_column(current_data, column_name, column_index, target_type, force_convert)
		TYPE_ARRAY:
			_convert_array_column(current_data, column_name, column_index, target_type, force_convert)

func _convert_dictionary_column(data: Dictionary, column_name: String, column_index: int, target_type: int, force_convert: bool = false) -> void:
	"""转换字典数据中的指定列"""
	if headers.size() > 0 and headers[0] == "ID":
		# 对象集合模式
		for key in data.keys():
			var obj = data[key]
			if typeof(obj) == TYPE_DICTIONARY and obj.has(column_name):
				var old_value = str(obj[column_name])
				var new_value = _convert_value_type(old_value, target_type, force_convert)
				obj[column_name] = new_value
				print("转换 ", key, ".", column_name, ": ", old_value, " -> ", new_value)
	else:
		# 键值对模式
		if column_index == 1:  # 值列
			for key in data.keys():
				var old_value = str(data[key])
				var new_value = _convert_value_type(old_value, target_type, force_convert)
				data[key] = new_value
				print("转换值 ", key, ": ", old_value, " -> ", new_value)

func _convert_array_column(data: Array, column_name: String, column_index: int, target_type: int, force_convert: bool = false) -> void:
	"""转换数组数据中的指定列"""
	if headers.size() == 2 and headers[0] == TranslationManager.get_text("index"):
		# 简单数组模式
		if column_index == 1:  # 值列
			for i in range(data.size()):
				var old_value = str(data[i])
				var new_value = _convert_value_type(old_value, target_type, force_convert)
				data[i] = new_value
				print("转换数组[", i, "]: ", old_value, " -> ", new_value)
	else:
		# 对象数组模式
		for item in data:
			if typeof(item) == TYPE_DICTIONARY and item.has(column_name):
				var old_value = str(item[column_name])
				var new_value = _convert_value_type(old_value, target_type, force_convert)
				item[column_name] = new_value
				print("转换对象.", column_name, ": ", old_value, " -> ", new_value)

func _infer_column_type(data: Dictionary, column_name: String, is_id_column: bool) -> int:
	"""推断列的数据类型"""
	if is_id_column:
		return 0  # ID列默认为字符串

	var sample_values = []
	for obj in data.values():
		if typeof(obj) == TYPE_DICTIONARY and obj.has(column_name):
			sample_values.append(obj[column_name])

	if sample_values.is_empty():
		return 0  # 默认为字符串

	# 检查是否所有值都是布尔类型
	var all_bool = true
	for value in sample_values:
		if typeof(value) != TYPE_BOOL:
			all_bool = false
			break
	if all_bool:
		return 2  # 布尔类型

	# 检查是否所有值都是数字类型
	var all_number = true
	for value in sample_values:
		if typeof(value) != TYPE_INT and typeof(value) != TYPE_FLOAT:
			all_number = false
			break
	if all_number:
		return 1  # 数字类型

	return 0  # 默认为字符串类型

func _infer_simple_values_type(values: Array) -> int:
	"""推断简单值列表的类型"""
	if values.is_empty():
		return 0  # 默认为字符串

	# 检查是否所有值都是布尔类型
	var all_bool = true
	for value in values:
		if typeof(value) != TYPE_BOOL:
			all_bool = false
			break
	if all_bool:
		return 2  # 布尔类型

	# 检查是否所有值都是数字类型
	var all_number = true
	for value in values:
		if typeof(value) != TYPE_INT and typeof(value) != TYPE_FLOAT:
			all_number = false
			break
	if all_number:
		return 1  # 数字类型

	return 0  # 默认为字符串类型

func _infer_array_column_type(data: Array, column_name: String) -> int:
	"""推断数组中对象某列的数据类型"""
	var sample_values = []
	for item in data:
		if typeof(item) == TYPE_DICTIONARY and item.has(column_name):
			sample_values.append(item[column_name])

	return _infer_simple_values_type(sample_values)

func _get_type_icon(column_index: int) -> String:
	"""获取列类型的图标"""
	if column_index >= column_types.size():
		return " [T]"  # 默认文本类型

	match column_types[column_index]:
		0:
			return " [T]"  # 文本/字符串
		1:
			return " [#]"  # 数字
		2:
			return " [✓]"  # 布尔值
		_:
			return " [?]"  # 未知类型

func get_column_type(column_index: int) -> int:
	"""获取指定列的类型"""
	if column_index >= 0 and column_index < column_types.size():
		return column_types[column_index]
	return 0  # 默认为字符串类型

func get_column_preview(column_index: int, target_type: int) -> String:
	"""获取列类型转换预览"""
	if column_index < 0 or column_index >= headers.size():
		return TranslationManager.get_text("invalid_column_index")

	var column_name = headers[column_index]
	var preview_lines = []
	var sample_count = 0
	var max_samples = 5  # 最多显示5个示例

	match typeof(current_data):
		TYPE_DICTIONARY:
			if headers.size() > 0 and headers[0] == "ID":
				# 对象集合模式
				for key in current_data.keys():
					if sample_count >= max_samples:
						break
					var obj = current_data[key]
					if typeof(obj) == TYPE_DICTIONARY and obj.has(column_name):
						var old_value = str(obj[column_name])
						var smart_value = _convert_value_type(old_value, target_type, false)
						var force_value = _convert_value_type(old_value, target_type, true)

						var smart_result = str(smart_value) + " (" + _get_type_name(typeof(smart_value)) + ")"
						var force_result = str(force_value) + " (" + _get_type_name(typeof(force_value)) + ")"

						var line = key + "." + column_name + ": \"" + old_value + "\""
						line += "\n  " + TranslationManager.get_text("smart_conversion_result") + " " + smart_result
						line += "\n  " + TranslationManager.get_text("force_conversion_result") + " " + force_result
						preview_lines.append(line)
						sample_count += 1
			else:
				# 键值对模式
				if column_index == 1:
					for key in current_data.keys():
						if sample_count >= max_samples:
							break
						var old_value = str(current_data[key])
						var smart_value = _convert_value_type(old_value, target_type, false)
						var force_value = _convert_value_type(old_value, target_type, true)

						var smart_result = str(smart_value) + " (" + _get_type_name(typeof(smart_value)) + ")"
						var force_result = str(force_value) + " (" + _get_type_name(typeof(force_value)) + ")"

						var line = key + ": \"" + old_value + "\""
						line += "\n  " + TranslationManager.get_text("smart_conversion") + " → " + smart_result
						line += "\n  " + TranslationManager.get_text("force_conversion") + " → " + force_result
						preview_lines.append(line)
						sample_count += 1
		TYPE_ARRAY:
			if headers.size() == 2 and headers[0] == TranslationManager.get_text("index"):
				# 简单数组模式
				if column_index == 1:
					for i in range(min(current_data.size(), max_samples)):
						var old_value = str(current_data[i])
						var smart_value = _convert_value_type(old_value, target_type, false)
						var force_value = _convert_value_type(old_value, target_type, true)

						var smart_result = str(smart_value) + " (" + _get_type_name(typeof(smart_value)) + ")"
						var force_result = str(force_value) + " (" + _get_type_name(typeof(force_value)) + ")"

						var line = "[" + str(i) + "]: \"" + old_value + "\""
						line += "\n  " + TranslationManager.get_text("smart_conversion") + " → " + smart_result
						line += "\n  " + TranslationManager.get_text("force_conversion") + " → " + force_result
						preview_lines.append(line)
			else:
				# 对象数组模式
				for i in range(min(current_data.size(), max_samples)):
					var item = current_data[i]
					if typeof(item) == TYPE_DICTIONARY and item.has(column_name):
						var old_value = str(item[column_name])
						var smart_value = _convert_value_type(old_value, target_type, false)
						var force_value = _convert_value_type(old_value, target_type, true)

						var smart_result = str(smart_value) + " (" + _get_type_name(typeof(smart_value)) + ")"
						var force_result = str(force_value) + " (" + _get_type_name(typeof(force_value)) + ")"

						var line = "[" + str(i) + "]." + column_name + ": \"" + old_value + "\""
						line += "\n  " + TranslationManager.get_text("smart_conversion_arrow") + smart_result
						line += "\n  " + TranslationManager.get_text("force_conversion_arrow") + force_result
						preview_lines.append(line)

	if preview_lines.is_empty():
		return TranslationManager.get_text("no_convertible_data_found")

	var result = TranslationManager.get_text("conversion_preview_header").replace("{count}", str(sample_count)) + ":\n\n"
	result += "\n\n".join(preview_lines)
	return result

func _get_type_name(type_id: int) -> String:
	"""获取Godot类型名称"""
	match type_id:
		TYPE_STRING:
			return TranslationManager.get_text("type_string")
		TYPE_INT:
			return TranslationManager.get_text("type_integer")
		TYPE_FLOAT:
			return TranslationManager.get_text("type_float")
		TYPE_BOOL:
			return TranslationManager.get_text("type_boolean")
		_:
			return TranslationManager.get_text("type_other")

# 行号单元格事件处理
func _on_row_number_input(event: InputEvent, row_number: int):
	if event is InputEventMouseButton:
		var mouse_event = event as InputEventMouseButton
		if mouse_event.pressed and mouse_event.button_index == MOUSE_BUTTON_RIGHT:
			_show_row_context_menu(row_number, mouse_event.global_position)

func _on_row_number_mouse_entered(panel: Panel, hover_style: StyleBoxFlat):
	panel.add_theme_stylebox_override("panel", hover_style)

func _on_row_number_mouse_exited(panel: Panel, normal_style: StyleBoxFlat):
	panel.add_theme_stylebox_override("panel", normal_style)

# 显示行操作的右键菜单
func _show_row_context_menu(row_number: int, position: Vector2):
	var popup = PopupMenu.new()
	popup.add_item(TranslationManager.get_text("add_new_row_before"), 0)
	popup.add_item(TranslationManager.get_text("add_new_row_after"), 1)
	popup.add_separator()
	popup.add_item(TranslationManager.get_text("copy_this_row"), 2)
	popup.add_separator()
	popup.add_item(TranslationManager.get_text("delete_this_row"), 3)

	popup.id_pressed.connect(func(id): _on_row_context_menu_selected(id, row_number))
	get_viewport().add_child(popup)
	popup.position = Vector2i(position)
	popup.popup()

	# 自动清理
	popup.popup_hide.connect(func(): popup.queue_free())

# 处理行操作菜单选择
func _on_row_context_menu_selected(id: int, row_number: int):
	print("菜单选择 - ID: ", id, ", 行号: ", row_number)
	match id:
		0: # 在此行之前添加
			print("执行：在此行之前添加")
			_add_row(row_number)
		1: # 在此行之后添加
			print("执行：在此行之后添加")
			_add_row(row_number + 1)
		2: # 复制此行
			print("执行：复制此行")
			_copy_row(row_number)
		3: # 删除此行
			print("执行：删除此行")
			_delete_row(row_number)

# 获取当前数据类型
func _get_data_type() -> String:
	match typeof(current_data):
		TYPE_DICTIONARY:
			# 检查是否为对象集合
			var all_objects = true
			for value in current_data.values():
				if typeof(value) != TYPE_DICTIONARY:
					all_objects = false
					break
			if all_objects and current_data.size() > 0:
				return "object_collection"
			else:
				return "key_value_pairs"
		TYPE_ARRAY:
			if current_data.is_empty():
				return "simple_array"
			var first_item = current_data[0]
			if typeof(first_item) == TYPE_DICTIONARY:
				return "object_array"
			else:
				return "simple_array"
		_:
			return "simple_value"

# 添加新行
func _add_row(row_index: int):
	print("添加行到索引: ", row_index)

	var data_type = _get_data_type()
	match data_type:
		"object_collection":
			_add_row_to_object_collection(row_index)
		"key_value_pairs":
			_add_row_to_key_value_pairs(row_index)
		"simple_array":
			_add_row_to_simple_array(row_index)
		"object_array":
			_add_row_to_object_array(row_index)

# 复制行
func _copy_row(row_index: int):
	print("复制行索引: ", row_index)

	if row_index < 0 or row_index >= rows_data.size():
		return

	var row_to_copy = rows_data[row_index]
	var data_type = _get_data_type()

	match data_type:
		"object_collection":
			_copy_row_in_object_collection(row_index, row_to_copy)
		"key_value_pairs":
			_copy_row_in_key_value_pairs(row_index, row_to_copy)
		"simple_array":
			_copy_row_in_simple_array(row_index, row_to_copy)
		"object_array":
			_copy_row_in_object_array(row_index, row_to_copy)

# 删除行
func _delete_row(row_index: int):
	print("删除行索引: ", row_index)

	if row_index < 0 or row_index >= rows_data.size():
		return

	# 确认对话框
	var dialog = ConfirmationDialog.new()
	dialog.dialog_text = TranslationManager.get_text("confirm_delete_row") % (row_index + 1)
	dialog.title = TranslationManager.get_text("confirm_delete")

	# 设置按钮文本
	dialog.ok_button_text = TranslationManager.get_text("confirm")
	dialog.cancel_button_text = TranslationManager.get_text("cancel")

	get_viewport().add_child(dialog)
	dialog.confirmed.connect(_confirm_delete_row.bind(row_index))
	dialog.canceled.connect(func(): dialog.queue_free())
	dialog.popup_centered()

	# 自动清理
	dialog.confirmed.connect(func(): dialog.queue_free(), CONNECT_ONE_SHOT)
	dialog.canceled.connect(func(): dialog.queue_free(), CONNECT_ONE_SHOT)

func _confirm_delete_row(row_index: int):
	var data_type = _get_data_type()
	match data_type:
		"object_collection":
			_delete_row_from_object_collection(row_index)
		"key_value_pairs":
			_delete_row_from_key_value_pairs(row_index)
		"simple_array":
			_delete_row_from_simple_array(row_index)
		"object_array":
			_delete_row_from_object_array(row_index)

# ===== 对象集合模式的行操作 =====
func _add_row_to_object_collection(row_index: int):
	print("对象集合模式 - 添加行到索引: ", row_index)

	# 对象集合模式的说明：Dictionary无法直接在指定位置插入
	# 但我们可以通过重建有序数据来模拟这个效果

	# 生成新的ID
	var new_id = _generate_unique_id()
	var new_obj = {}

	# 为新对象创建默认值
	for i in range(1, headers.size()):  # 跳过ID列
		var column_name = headers[i]
		new_obj[column_name] = _get_default_value_for_type(column_types[i])

	# 创建新的有序数据字典，基于当前表格显示的行顺序
	var new_data = {}

	# 获取当前表格中实际显示的行顺序（从rows_data中获取ID）
	var current_row_keys: Array[String] = []
	for row in rows_data:
		if row.size() > 0:
			current_row_keys.append(row[0])  # 第一列是ID

	# 根据row_index插入新项目
	var inserted = false
	for i in range(current_row_keys.size()):
		if i == row_index and not inserted:
			new_data[new_id] = new_obj
			inserted = true
			print("在位置 ", i, " 插入新对象，ID: ", new_id)

		var key = current_row_keys[i]
		new_data[key] = current_data[key]

	# 如果还没有插入（row_index超出范围），则添加到末尾
	if not inserted:
		new_data[new_id] = new_obj
		print("在末尾添加新对象，ID: ", new_id)

	# 更新数据
	current_data = new_data
	print("已添加新对象，ID: ", new_id, ", 数据: ", new_obj)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

func _copy_row_in_object_collection(row_index: int, row_to_copy: Array):
	print("对象集合模式 - 复制行索引: ", row_index)

	if row_to_copy.is_empty():
		print("错误：要复制的行数据为空")
		return

	var original_id = row_to_copy[0]
	var original_obj = current_data.get(original_id)
	if not original_obj:
		print("错误：找不到原始对象，ID: ", original_id)
		return

	# 生成新的ID
	var new_id = _generate_unique_id()
	var new_obj = original_obj.duplicate(true)

	# 添加到数据中
	current_data[new_id] = new_obj
	print("已复制对象，原ID: ", original_id, ", 新ID: ", new_id)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

func _delete_row_from_object_collection(row_index: int):
	print("对象集合模式 - 删除行索引: ", row_index)

	if row_index >= rows_data.size():
		print("错误：行索引超出范围: ", row_index, "/", rows_data.size())
		return

	var id_to_delete = rows_data[row_index][0]
	current_data.erase(id_to_delete)
	print("已删除对象，ID: ", id_to_delete)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

# ===== 键值对模式的行操作 =====
func _add_row_to_key_value_pairs(row_index: int):
	print(TranslationManager.get_text("key_value_pair_mode"), " - ", TranslationManager.get_text("add_row_to_index"), ": ", row_index)

	var new_key = _generate_unique_key()
	var new_value = _get_default_value_for_type(column_types[1])

	# 创建新的有序数据字典，基于当前表格显示的行顺序
	var new_data = {}

	# 获取当前表格中实际显示的行顺序（从rows_data中获取键）
	var current_row_keys: Array[String] = []
	for row in rows_data:
		if row.size() > 0:
			current_row_keys.append(row[0])  # 第一列是键

	# 根据row_index插入新键值对
	var inserted = false
	for i in range(current_row_keys.size()):
		if i == row_index and not inserted:
			new_data[new_key] = new_value
			inserted = true
			print(TranslationManager.get_text("insert_new_key_value_pair"), " ", i, ", ", TranslationManager.get_text("key"), ": ", new_key)

		var key = current_row_keys[i]
		new_data[key] = current_data[key]

	# 如果还没有插入，则添加到末尾
	if not inserted:
		new_data[new_key] = new_value
		print(TranslationManager.get_text("add_at_end"), ", ", TranslationManager.get_text("key"), ": ", new_key)

	# 更新数据
	current_data = new_data
	print("已添加新键值对，键: ", new_key, ", 值: ", new_value)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

func _copy_row_in_key_value_pairs(row_index: int, row_to_copy: Array):
	print(TranslationManager.get_text("key_value_pair_mode"), " - ", TranslationManager.get_text("copy_key_value_pair"), ": ", row_index)

	if row_to_copy.size() < 2:
		print("错误：要复制的行数据不完整")
		return

	var original_key = row_to_copy[0]
	var original_value = current_data.get(original_key)

	var new_key = _generate_unique_key()
	current_data[new_key] = original_value
	print("已复制键值对，原键: ", original_key, ", 新键: ", new_key, ", 值: ", original_value)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

func _delete_row_from_key_value_pairs(row_index: int):
	print(TranslationManager.get_text("key_value_pair_mode"), " - ", TranslationManager.get_text("delete_key_value_pair"), ": ", row_index)

	if row_index >= rows_data.size():
		print("错误：行索引超出范围: ", row_index, "/", rows_data.size())
		return

	var key_to_delete = rows_data[row_index][0]
	current_data.erase(key_to_delete)
	print("已删除键值对，键: ", key_to_delete)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

# ===== 简单数组模式的行操作 =====
func _add_row_to_simple_array(row_index: int):
	print("简单数组模式 - 添加行到索引: ", row_index)

	var new_value = _get_default_value_for_type(column_types[1])

	if row_index >= current_data.size():
		current_data.append(new_value)
		print(TranslationManager.get_text("add_new_value_to_array"), ": ", new_value)
	else:
		current_data.insert(row_index, new_value)
		print(TranslationManager.get_text("insert_new_value_at_index"), " ", row_index, ": ", new_value)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

func _copy_row_in_simple_array(row_index: int, row_to_copy: Array):
	print("简单数组模式 - 复制行索引: ", row_index)

	if row_index >= current_data.size():
		print("错误：行索引超出范围: ", row_index, "/", current_data.size())
		return

	if row_to_copy.size() < 2:
		print("错误：要复制的行数据不完整")
		return

	var value_to_copy = current_data[row_index]
	current_data.insert(row_index + 1, value_to_copy)
	print(TranslationManager.get_text("copy_array_element"), ", ", TranslationManager.get_text("index"), " ", row_index, " ", TranslationManager.get_text("value"), " ", value_to_copy, " ", TranslationManager.get_text("to_index"), " ", row_index + 1)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

func _delete_row_from_simple_array(row_index: int):
	print("简单数组模式 - 删除行索引: ", row_index)

	if row_index >= current_data.size():
		print("错误：行索引超出范围: ", row_index, "/", current_data.size())
		return

	var deleted_value = current_data[row_index]
	current_data.remove_at(row_index)
	print(TranslationManager.get_text("delete_array_element"), ", ", TranslationManager.get_text("index"), " ", row_index, " ", TranslationManager.get_text("value"), ": ", deleted_value)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

# ===== 对象数组模式的行操作 =====
func _add_row_to_object_array(row_index: int):
	print("对象数组模式 - 添加行到索引: ", row_index)

	var new_obj = {}

	# 为新对象创建默认值
	for i in range(headers.size()):
		var column_name = headers[i]
		new_obj[column_name] = _get_default_value_for_type(column_types[i])

	if row_index >= current_data.size():
		current_data.append(new_obj)
		print("已在数组末尾添加新对象: ", new_obj)
	else:
		current_data.insert(row_index, new_obj)
		print("已在索引 ", row_index, " 插入新对象: ", new_obj)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

func _copy_row_in_object_array(row_index: int, row_to_copy: Array):
	print("对象数组模式 - 复制行索引: ", row_index)

	if row_index >= current_data.size():
		print("错误：行索引超出范围: ", row_index, "/", current_data.size())
		return

	var obj_to_copy = current_data[row_index]
	if typeof(obj_to_copy) == TYPE_DICTIONARY:
		var new_obj = obj_to_copy.duplicate(true)
		current_data.insert(row_index + 1, new_obj)
		print("已复制对象，从索引 ", row_index, " 到索引 ", row_index + 1, ", 对象: ", new_obj)

		# 重建表格
		await setup_data(current_data)
		data_changed.emit(current_data)
	else:
		print("错误：要复制的元素不是对象类型")

func _delete_row_from_object_array(row_index: int):
	print("对象数组模式 - 删除行索引: ", row_index)

	if row_index >= current_data.size():
		print("错误：行索引超出范围: ", row_index, "/", current_data.size())
		return

	var deleted_obj = current_data[row_index]
	current_data.remove_at(row_index)
	print("已删除对象，索引 ", row_index, " 的对象: ", deleted_obj)

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)

# ===== 辅助函数 =====
func _generate_unique_id() -> String:
	var base_name = "new_item"
	var counter = 1

	while current_data.has(base_name + "_" + str(counter)):
		counter += 1

	return base_name + "_" + str(counter)

func _generate_unique_key() -> String:
	var base_name = "new_key"
	var counter = 1

	while current_data.has(base_name + "_" + str(counter)):
		counter += 1

	return base_name + "_" + str(counter)

func _get_default_value_for_type(type_id: int) -> Variant:
	match type_id:
		0: # String
			return TranslationManager.get_text("new_value_default")
		1: # Number
			return 0
		2: # Boolean
			return false
		_:
			return TranslationManager.get_text("new_value_default")

# ===== 列标题编辑辅助函数 =====
func can_edit_column_name(column_index: int) -> bool:
	"""检查是否可以编辑指定列的名称"""
	var data_type = _get_data_type()

	match data_type:
		"object_collection":
			# 对象集合模式下，ID列不能编辑
			return column_index != 0
		"key_value_pairs":
			# 键值对模式下，键和值列都不能编辑（因为是固定结构）
			return false
		"simple_array":
			# 简单数组模式下，索引和值列都不能编辑
			return false
		"object_array":
			# 对象数组模式下，所有列都可以编辑
			return true
		_:
			return false

func rename_column(column_index: int, new_name: String) -> bool:
	"""重命名列"""
	if column_index >= headers.size():
		return false

	# 检查名称是否已存在
	if new_name in headers and headers.find(new_name) != column_index:
		return false

	var old_name = headers[column_index]
	print("重命名列 ", column_index, ": '", old_name, "' -> '", new_name, "'")

	# 更新数据中的键名
	_update_data_column_names(old_name, new_name)

	# 更新标题
	headers[column_index] = new_name

	# 重建表格
	await setup_data(current_data)
	data_changed.emit(current_data)
	return true

func _update_data_column_names(old_name: String, new_name: String):
	"""更新数据中的列名"""
	var data_type = _get_data_type()

	match data_type:
		"object_collection":
			# 对象集合模式：更新所有对象中的键名
			for key in current_data.keys():
				var obj = current_data[key]
				if typeof(obj) == TYPE_DICTIONARY and obj.has(old_name):
					var value = obj[old_name]
					obj.erase(old_name)
					obj[new_name] = value
		"object_array":
			# 对象数组模式：更新数组中所有对象的键名
			for item in current_data:
				if typeof(item) == TYPE_DICTIONARY and item.has(old_name):
					var value = item[old_name]
					item.erase(old_name)
					item[new_name] = value

# 简单的单元格展开对话框
func _show_simple_expansion_dialog(row: int, col: int, content: String, source_cell: LineEdit):
	print("创建简单展开对话框")

	# 创建对话框
	var dialog = ConfirmationDialog.new()
	dialog.title = TranslationManager.get_text("cell_content_viewer") % [row + 1, col + 1]
	dialog.size = Vector2(600, 400)

	# 创建文本编辑区域
	var text_edit = TextEdit.new()
	text_edit.text = content
	text_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	text_edit.size_flags_vertical = Control.SIZE_EXPAND_FILL

	# 添加到对话框
	dialog.add_child(text_edit)

	# 设置按钮文本
	dialog.ok_button_text = TranslationManager.get_text("confirm")
	dialog.cancel_button_text = TranslationManager.get_text("cancel")

	# 连接信号
	dialog.confirmed.connect(func():
		print("用户确认，更新单元格内容")
		source_cell.text = text_edit.text
		source_cell.text_submitted.emit(text_edit.text)
		dialog.queue_free()
	)

	dialog.canceled.connect(func():
		print("用户取消")
		dialog.queue_free()
	)

	# 添加到场景并显示
	get_viewport().add_child(dialog)
	dialog.popup_centered()

	print("简单展开对话框已显示")

# 展开区域相关函数
func _setup_expansion_button_styles(cancel_button: Button, confirm_button: Button):
	"""设置展开区域按钮样式"""
	# 取消按钮样式
	var cancel_normal = StyleBoxFlat.new()
	cancel_normal.bg_color = Color("#F3F4F6")
	cancel_normal.border_width_left = 1
	cancel_normal.border_width_top = 1
	cancel_normal.border_width_right = 1
	cancel_normal.border_width_bottom = 1
	cancel_normal.border_color = Color("#D1D5DB")
	cancel_normal.corner_radius_top_left = 4
	cancel_normal.corner_radius_top_right = 4
	cancel_normal.corner_radius_bottom_left = 4
	cancel_normal.corner_radius_bottom_right = 4
	cancel_button.add_theme_stylebox_override("normal", cancel_normal)
	cancel_button.add_theme_color_override("font_color", Color("#374151"))

	# 确认按钮样式
	var confirm_normal = StyleBoxFlat.new()
	confirm_normal.bg_color = Color("#4A90E2")
	confirm_normal.border_width_left = 1
	confirm_normal.border_width_top = 1
	confirm_normal.border_width_right = 1
	confirm_normal.border_width_bottom = 1
	confirm_normal.border_color = Color("#3B82F6")
	confirm_normal.corner_radius_top_left = 4
	confirm_normal.corner_radius_top_right = 4
	confirm_normal.corner_radius_bottom_left = 4
	confirm_normal.corner_radius_bottom_right = 4
	confirm_button.add_theme_stylebox_override("normal", confirm_normal)
	confirm_button.add_theme_color_override("font_color", Color.WHITE)

func _expand_cell_content(row: int, col: int, content: String, source_cell: LineEdit):
	"""展开单元格内容到浮动窗口"""
	print(TranslationManager.get_text("expand_cell_content_floating"), ":", row, " ", TranslationManager.get_text("column_text"), ":", col)

	# 保存当前展开的单元格信息
	current_expanded_cell = {
		"row": row,
		"col": col,
		"source_cell": source_cell,
		"original_content": content
	}

	# 创建浮动展开器窗口
	universal_expander = UniversalCellExpander.new()
	get_tree().root.add_child(universal_expander)

	# 连接信号
	universal_expander.content_updated.connect(_on_expansion_content_updated)
	universal_expander.expansion_closed.connect(_on_expansion_closed)

	# 设置展开信息
	var cell_info = {
		"title": TranslationManager.get_text("cell_content_title") % [row + 1, col + 1, headers[col] if col < headers.size() else ""]
	}

	# 设置窗口标题
	universal_expander.title = TranslationManager.get_text("cell_expansion_title") % [row + 1, col + 1]

	# 设置展开内容并显示
	universal_expander.setup_expansion(content, cell_info, 0)
	universal_expander.show_expansion()

# 旧的展开函数已被通用展开器替代
