@tool
extends RefCounted
class_name CellExpander

# 引入翻译管理器
const TranslationManager = preload("res://addons/json_editor/scripts/translation_manager.gd")

# 信号
signal cell_expansion_requested(row: int, col: int, content: String)
signal cell_collapsed()

# 静态变量，用于跟踪当前展开的单元格
static var current_expanded_cell: Dictionary = {}
static var expansion_dialog: ConfirmationDialog = null

# 单元格展开管理器
class CellExpansionManager:
	var table_ref: WeakRef
	var expanded_cells: Dictionary = {}
	var expansion_overlay: Control = null
	
	func _init(table: Control):
		table_ref = weakref(table)
	
	func setup_cell_expansion(line_edit: LineEdit, row: int, col: int) -> void:
		"""为单元格设置双击展开功能"""
		if not line_edit:
			return

		# 添加展开提示到tooltip
		var original_tooltip = line_edit.tooltip_text
		var expand_hint = TranslationManager.get_text("double_click_to_expand")
		if original_tooltip.is_empty():
			line_edit.tooltip_text = expand_hint
		else:
			line_edit.tooltip_text = original_tooltip + "\n" + expand_hint
	
	func _show_expansion_dialog(row: int, col: int, content: String, source_cell: LineEdit) -> void:
		"""显示单元格内容展开对话框"""
		print("开始创建展开对话框")
		var table = table_ref.get_ref()
		if not table:
			print("错误：无法获取表格引用")
			return

		print("表格引用获取成功")

		# 关闭之前的对话框
		_close_current_dialog()

		# 创建新的展开对话框
		var dialog = ConfirmationDialog.new()
		dialog.title = TranslationManager.get_text("cell_content_viewer") % [str(row + 1), str(col + 1)]
		dialog.size = Vector2(600, 400)
		dialog.unresizable = false

		# 设置按钮文本
		dialog.ok_button_text = TranslationManager.get_text("confirm")
		dialog.cancel_button_text = TranslationManager.get_text("cancel")

		print("对话框创建完成，标题：", dialog.title)
		
		# 创建主容器
		var vbox = VBoxContainer.new()
		vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		vbox.add_theme_constant_override("separation", 10)
		dialog.add_child(vbox)
		
		# 添加信息标签
		var info_label = Label.new()
		info_label.text = TranslationManager.get_text("cell_position_info") % [str(row + 1), str(col + 1)]
		info_label.add_theme_font_size_override("font_size", 12)
		info_label.add_theme_color_override("font_color", Color("#666666"))
		vbox.add_child(info_label)
		
		# 创建文本编辑区域
		var text_edit = TextEdit.new()
		text_edit.text = content
		text_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		text_edit.size_flags_vertical = Control.SIZE_EXPAND_FILL
		text_edit.placeholder_text = TranslationManager.get_text("enter_cell_content")
		
		# 设置文本编辑器样式
		_setup_text_edit_style(text_edit)
		
		vbox.add_child(text_edit)

		# 设置对话框按钮文本
		dialog.ok_button_text = TranslationManager.get_text("confirm")
		dialog.cancel_button_text = TranslationManager.get_text("cancel")
		
		# 添加到场景并显示
		print("准备添加对话框到视口")
		var viewport = table.get_viewport()
		if not viewport:
			print("错误：无法获取视口")
			return

		print("视口获取成功，添加对话框")
		viewport.add_child(dialog)
		print("对话框已添加到视口，准备显示")

		dialog.popup_centered()
		dialog.show()
		print("对话框显示命令已执行")

		# 聚焦到文本编辑器
		text_edit.grab_focus()
		text_edit.select_all()

		# 保存当前对话框引用
		CellExpander.expansion_dialog = dialog
		CellExpander.current_expanded_cell = {"row": row, "col": col, "dialog": dialog}

		# 连接关闭事件
		dialog.canceled.connect(_on_dialog_closed)
		dialog.confirmed.connect(func(): _apply_cell_content(text_edit.text, source_cell, dialog))

		print("展开对话框设置完成")
	
	func _setup_text_edit_style(text_edit: TextEdit) -> void:
		"""设置文本编辑器样式"""
		# 普通状态样式
		var normal_style = StyleBoxFlat.new()
		normal_style.bg_color = Color.WHITE
		normal_style.border_width_left = 2
		normal_style.border_width_top = 2
		normal_style.border_width_right = 2
		normal_style.border_width_bottom = 2
		normal_style.border_color = Color("#E1E5E9")
		normal_style.corner_radius_top_left = 4
		normal_style.corner_radius_top_right = 4
		normal_style.corner_radius_bottom_left = 4
		normal_style.corner_radius_bottom_right = 4
		normal_style.content_margin_left = 8
		normal_style.content_margin_right = 8
		normal_style.content_margin_top = 8
		normal_style.content_margin_bottom = 8
		text_edit.add_theme_stylebox_override("normal", normal_style)
		
		# 焦点状态样式
		var focus_style = StyleBoxFlat.new()
		focus_style.bg_color = Color.WHITE
		focus_style.border_width_left = 2
		focus_style.border_width_top = 2
		focus_style.border_width_right = 2
		focus_style.border_width_bottom = 2
		focus_style.border_color = Color("#4A90E2")
		focus_style.corner_radius_top_left = 4
		focus_style.corner_radius_top_right = 4
		focus_style.corner_radius_bottom_left = 4
		focus_style.corner_radius_bottom_right = 4
		focus_style.content_margin_left = 8
		focus_style.content_margin_right = 8
		focus_style.content_margin_top = 8
		focus_style.content_margin_bottom = 8
		text_edit.add_theme_stylebox_override("focus", focus_style)
		
		# 字体设置
		text_edit.add_theme_font_size_override("font_size", 13)
		text_edit.add_theme_color_override("font_color", Color("#333333"))
	
	func _setup_button_styles(cancel_button: Button, confirm_button: Button) -> void:
		"""设置按钮样式"""
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
		
		# 设置按钮大小
		cancel_button.custom_minimum_size = Vector2(80, 32)
		confirm_button.custom_minimum_size = Vector2(80, 32)
	
	func _apply_cell_content(new_content: String, source_cell: LineEdit, dialog: ConfirmationDialog) -> void:
		"""应用编辑后的内容到原单元格"""
		if source_cell and is_instance_valid(source_cell):
			source_cell.text = new_content
			# 触发文本提交事件，让表格更新数据
			source_cell.text_submitted.emit(new_content)
		
		dialog.hide()
	
	func _on_dialog_closed() -> void:
		"""对话框关闭时的清理工作"""
		if CellExpander.expansion_dialog:
			CellExpander.expansion_dialog.queue_free()
			CellExpander.expansion_dialog = null
		CellExpander.current_expanded_cell.clear()
	
	func _close_current_dialog() -> void:
		"""关闭当前打开的展开对话框"""
		if CellExpander.expansion_dialog and is_instance_valid(CellExpander.expansion_dialog):
			CellExpander.expansion_dialog.hide()

# 静态方法，用于创建和管理单元格展开功能
static func create_expansion_manager(table: Control) -> CellExpansionManager:
	"""创建单元格展开管理器"""
	return CellExpansionManager.new(table)

static func is_content_expandable(content: String) -> bool:
	"""检查内容是否适合展开"""
	return content.length() > 50 or content.count("\n") > 0 or content.count("\t") > 0

static func get_content_preview(content: String, max_length: int = 50) -> String:
	"""获取内容预览"""
	if content.length() <= max_length:
		return content
	
	return content.substr(0, max_length) + "..."
