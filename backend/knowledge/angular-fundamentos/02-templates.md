---
slug: angular-templates
title: Templates e Interpolação
order: 2
level: Beginner
tags: [template, interpolation, expression, syntax]
readMinutes: 7
---

## Para que serve

O template é o HTML que descreve a interface visual de um componente Angular. Templates Angular estendem o HTML padrão com sintaxe especial para interpolação, ligação de dados, expressões e fluxo de controle. O motor de renderização do Angular transforma o template em instruções que atualizam o DOM de forma eficiente sempre que o estado do componente muda.

## Interpolação com chaves duplas

A interpolação é a forma mais simples de exibir valores do componente no template. A sintaxe `{{ expressao }}` é avaliada em tempo de execução e o resultado é convertido em string e inserido no DOM. A interpolação aceita qualquer expressão JavaScript válida, incluindo chamadas de método, operadores aritméticos e acesso a propriedades.

```ts
@Component({
  selector: 'app-perfil',
  standalone: true,
  template: `
    <h2>{{ usuario.nome }}</h2>
    <p>Email: {{ usuario.email.toLowerCase() }}</p>
    <p>Idade: {{ anoAtual - usuario.anoNascimento }} anos</p>
    <p>Status: {{ ativo ? 'Online' : 'Offline' }}</p>
  `
})
export class PerfilComponent {
  usuario = { nome: 'Ana', email: 'Ana@Email.com', anoNascimento: 1992 };
  anoAtual = 2026;
  ativo = true;
}
```

A interpolação só funciona no conteúdo de elementos e em atributos como string. Para alterar propriedades do DOM diretamente, é necessário usar property binding com colchetes.

## Expressões de template — o que é permitido

Expressões em templates Angular são parecidas com expressões JavaScript, mas com restrições importantes. Operadores de atribuição (`=`, `+=`, `-=`) não são permitidos no corpo das interpolações. Os operadores `new`, `typeof` e `instanceof` também são proibidos. Não é possível usar incremento (`++`) nem decremento (`--`) dentro de expressões.

O motivo dessas restrições é manter os templates declarativos e previsíveis. Expressões devem ler estado, não modificá-lo. Modificações de estado pertencem a métodos da classe do componente, que são acionados por event bindings.

```html
<!-- válido -->
<p>{{ items.length }}</p>
<p>{{ pessoas | json }}</p>
<p>{{ calcularTotal(carrinho) }}</p>

<!-- inválido — gera erro de compilação -->
<p>{{ contador++ }}</p>
<p>{{ x = 10 }}</p>
```

## Novo controle de fluxo: @if, @for, @switch

A partir do Angular 17, foram introduzidas novas palavras-chave para fluxo de controle diretamente na sintaxe do template, sem precisar de diretivas estruturais. Essas formas são mais rápidas em runtime e oferecem melhor experiência de desenvolvimento.

```html
@if (usuario.logado) {
  <p>Bem-vindo, {{ usuario.nome }}</p>
} @else {
  <a routerLink="/login">Faça login</a>
}

@for (item of carrinho; track item.id) {
  <li>{{ item.nome }} — R$ {{ item.preco }}</li>
} @empty {
  <p>Seu carrinho está vazio.</p>
}

@switch (status) {
  @case ('pendente') { <span class="tag tag-warning">Pendente</span> }
  @case ('aprovado') { <span class="tag tag-success">Aprovado</span> }
  @default { <span class="tag tag-inactive">Desconhecido</span> }
}
```

A função `track` em `@for` é obrigatória e serve para o Angular identificar cada item de forma única. Sem `track`, a performance de listas grandes degrada significativamente porque o framework precisa recriar todos os nós DOM a cada atualização.

## Referências de template com hash

Uma referência de template é uma variável criada no template usando o prefixo `#`. Essa referência aponta para o elemento DOM, para uma diretiva ou para um componente filho. A referência pode ser usada em qualquer lugar do mesmo template, permitindo interações entre elementos sem passar pela classe do componente.

```html
<input #campoEmail type="email" placeholder="Seu email" />
<button (click)="enviar(campoEmail.value)">Enviar</button>

<p>Tamanho atual: {{ campoEmail.value.length }}</p>
```

Quando aplicada a um elemento HTML padrão, a referência aponta para o `HTMLElement` correspondente. Quando aplicada a um componente Angular, aponta para a instância do componente, permitindo acesso a métodos e propriedades públicas.

## Atributos especiais: class e style

Templates Angular oferecem ligações otimizadas para classes CSS e estilos inline. A sintaxe `[class.nome]="condicao"` adiciona ou remove uma classe com base em uma expressão booleana. A sintaxe `[style.propriedade]="valor"` aplica um estilo inline dinamicamente.

```html
<div
  [class.ativo]="estaSelecionado"
  [class.destaque]="prioridade === 'alta'"
  [style.color]="cor"
  [style.font-size.px]="tamanho">
  Item dinâmico
</div>
```

A unidade pode ser anexada diretamente ao nome da propriedade de estilo, como em `[style.width.%]` ou `[style.margin.rem]`. Essa forma evita concatenação manual de strings com unidades.

## Comentários e segurança

Comentários HTML padrão (`<!-- ... -->`) são preservados pelo Angular, mas não são processados como expressões. Para comentar uma seção de template e remover sua renderização condicionalmente, use `@if (false)` ou comente o bloco com sintaxe HTML.

O Angular sanitiza automaticamente valores interpolados em contextos perigosos como `innerHTML`, prevenindo ataques de cross-site scripting. Quando você precisa renderizar HTML confiável, deve usar o serviço `DomSanitizer` explicitamente, marcando o conteúdo como seguro. Nunca contorne a sanitização sem entender as implicações de segurança.

## Armadilhas comuns

Um erro frequente é tentar usar `if`, `for` ou `switch` do JavaScript dentro de interpolações — essas estruturas não são expressões e o template não as aceita. Use `@if`, `@for` ou o operador ternário no lugar. Outro problema é assumir que métodos chamados em interpolações executam só uma vez; na verdade, esses métodos rodam a cada ciclo de detecção de mudanças, então devem ser leves e puros.
