---
slug: angular-template-forms
title: Template-driven Forms
order: 9
level: Beginner
tags: [forms, ngmodel, validation, ngform, template-driven]
readMinutes: 8
---

## Para que serve

Template-driven forms é a abordagem do Angular para construir formulários cujo modelo é definido principalmente no template HTML, com lógica mínima na classe do componente. Diretivas como `ngModel`, `ngForm` e `ngModelGroup` constroem automaticamente um `FormGroup` por trás dos panos. Essa abordagem é ideal para formulários simples e de tamanho pequeno a médio, com regras de validação declarativas no HTML.

## FormsModule e o setup inicial

Para usar template-driven forms, é necessário importar o `FormsModule` do `@angular/forms`. Em componentes standalone, basta adicioná-lo ao array `imports` do `@Component`. O `FormsModule` registra a diretiva `ngModel`, `ngForm` e as diretivas de validação padrão.

```ts
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-cadastro',
  standalone: true,
  imports: [FormsModule],
  template: `
    <form #formulario="ngForm" (ngSubmit)="enviar(formulario)">
      <input name="nome" [(ngModel)]="dados.nome" required minlength="3" />
      <input name="email" type="email" [(ngModel)]="dados.email" required email />
      <button type="submit" [disabled]="formulario.invalid">Enviar</button>
    </form>
  `
})
export class CadastroComponent {
  dados = { nome: '', email: '' };

  enviar(form: any) {
    if (form.valid) {
      console.log('Enviando:', this.dados);
    }
  }
}
```

A diretiva `#formulario="ngForm"` cria uma referência ao `NgForm` que o Angular instancia automaticamente em todo `<form>`. Esse objeto expõe estado de validação, valores e métodos como `reset()`.

## ngModel — o coração do template-driven

O `ngModel` é a diretiva que faz a ligação bidirecional entre o input e a propriedade do modelo. Quando usado dentro de um `<form>`, ele também registra o controle no `NgForm` pai, contribuindo para o estado geral do formulário. Cada input com `ngModel` deve ter um atributo `name` único; sem ele, o registro no formulário falha.

A sintaxe `[(ngModel)]="propriedade"` ativa a ligação two-way. Se você só quer reagir a mudanças sem ligação automática, pode usar `(ngModelChange)="metodo($event)"` ou `[ngModel]="valor"` separadamente.

```html
<input
  name="senha"
  type="password"
  [(ngModel)]="dados.senha"
  required
  minlength="8"
  #senhaControl="ngModel" />

@if (senhaControl.invalid && senhaControl.touched) {
  <p class="alert alert-danger">
    @if (senhaControl.errors?.['required']) { Campo obrigatório. }
    @if (senhaControl.errors?.['minlength']) { Mínimo 8 caracteres. }
  </p>
}
```

A referência local `#senhaControl="ngModel"` expõe a instância `NgModel`, que carrega informações como `valid`, `invalid`, `touched`, `dirty`, `pristine`, `errors` e `value`.

## Validadores built-in

O Angular oferece um conjunto de diretivas de validação que podem ser aplicadas diretamente nos inputs. As mais comuns são `required`, `minlength`, `maxlength`, `min`, `max`, `pattern` e `email`. Cada validador adiciona uma chave ao objeto `errors` quando a validação falha.

```html
<input name="idade" type="number" [(ngModel)]="dados.idade"
       required min="18" max="120" />

<input name="cep" [(ngModel)]="dados.cep"
       required pattern="\d{5}-?\d{3}" />

<input name="email" type="email" [(ngModel)]="dados.email"
       required email />
```

Esses validadores rodam toda vez que o valor do input muda. O estado de validade é propagado para o `NgForm` pai, que reflete `valid` apenas quando todos os controles são válidos.

## Estados do controle

Cada controle de formulário tem três pares de estados que ajudam a controlar a exibição de mensagens. `touched`/`untouched` indicam se o usuário interagiu com o campo (saiu do foco pelo menos uma vez). `dirty`/`pristine` indicam se o valor foi alterado. `valid`/`invalid` refletem o resultado das validações.

Esses estados também são adicionados ao DOM como classes CSS: `ng-touched`, `ng-untouched`, `ng-dirty`, `ng-pristine`, `ng-valid`, `ng-invalid`. Você pode estilizar campos inválidos globalmente com `.ng-invalid.ng-touched { border-color: var(--color-danger-default); }`.

A combinação mais comum para mostrar mensagens de erro é `controle.invalid && controle.touched`. Isso evita marcar como erro um campo que o usuário ainda não tocou.

## ngModelGroup — agrupando campos

Quando o formulário tem subgrupos lógicos, como endereço ou contato, a diretiva `ngModelGroup` permite organizar esses controles em um sub-grupo. O estado e os valores do grupo ficam aninhados no `NgForm` pai, refletindo a estrutura.

```html
<form #form="ngForm">
  <input name="nome" [(ngModel)]="dados.nome" required />

  <fieldset ngModelGroup="endereco" #endereco="ngModelGroup">
    <input name="rua" [(ngModel)]="dados.endereco.rua" required />
    <input name="cidade" [(ngModel)]="dados.endereco.cidade" required />
    <input name="cep" [(ngModel)]="dados.endereco.cep" required />
  </fieldset>

  @if (endereco.invalid && endereco.touched) {
    <p>Preencha o endereço completo.</p>
  }
</form>
```

Ao enviar o formulário, `form.value` retorna um objeto com a estrutura `{ nome, endereco: { rua, cidade, cep } }`, refletindo a hierarquia declarada no template.

## Submissão e reset

O evento `(ngSubmit)` é disparado quando o formulário é submetido por um botão `type="submit"` ou pela tecla Enter em um input. Use `ngSubmit` em vez do evento nativo `submit` para que o Angular cuide da prevenção do comportamento padrão do navegador.

```ts
@Component({
  selector: 'app-contato',
  standalone: true,
  imports: [FormsModule],
  template: `
    <form #f="ngForm" (ngSubmit)="enviar(f)">
      <input name="mensagem" [(ngModel)]="msg" required />
      <button type="submit" [disabled]="f.invalid">Enviar</button>
      <button type="button" (click)="f.resetForm()">Limpar</button>
    </form>
  `
})
export class ContatoComponent {
  msg = '';
  enviar(form: any) {
    if (form.valid) {
      console.log('Mensagem:', this.msg);
      form.resetForm();
    }
  }
}
```

O método `resetForm()` limpa valores e devolve o formulário ao estado `pristine` e `untouched`, removendo também marcações visuais de validação.

## Template-driven vs Reactive Forms

Template-driven forms são mais simples e adequados a formulários pequenos e com pouca lógica dinâmica. Reactive forms, baseados em `FormGroup`/`FormControl` declarados na classe, oferecem mais controle programático, melhor testabilidade e tipagem mais robusta — são preferidos para formulários complexos, dinâmicos ou que mudam estrutura em runtime.

A escolha não é exclusiva; um mesmo projeto pode misturar as duas abordagens. A regra geral é: se o formulário cabe em uma tela e tem regras estáticas, use template-driven; se tem muitos campos dinâmicos, validações cross-field ou precisa ser testado intensivamente, use reactive.

## Armadilhas comuns

Esquecer o atributo `name` em inputs com `ngModel` impede o registro no formulário e quebra o estado de validação. Outro erro frequente é mostrar mensagens de erro com base apenas em `invalid`, sem combinar com `touched` — o usuário vê erros antes mesmo de digitar. Por fim, lembre que `[(ngModel)]` cria uma cópia bidirecional do valor, então mutar diretamente uma propriedade do modelo já reflete no input sem reatribuição.
